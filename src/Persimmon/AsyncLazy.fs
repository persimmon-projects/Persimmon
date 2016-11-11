namespace Microsoft.FSharp.Control

open System
open System.Collections.Generic
open System.Threading

///////////////////////////////////////////////////////////////////////////////////

/// <summary>
/// Delegation F#'s async continuation.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
/// <description>
/// Simulate TaskCompletionSource&lt;'T&gt; for F#'s Async&lt;'T&gt;.
/// </description>
/// <typeparam name="'T">Computation result type</typeparam>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type private AsyncCompletionSource<'T> =

  [<DefaultValue>]
  val mutable private _value : 'T option
  [<DefaultValue>]
  val mutable private _exn : exn option

  [<DefaultValue>]
  val mutable private _completed : ('T -> unit) option
  [<DefaultValue>]
  val mutable private _caught : (exn -> unit) option

  val private _async : Async<'T>

  /// <summary>
  /// Constructor.
  /// </summary>
  new () as this = {
    _async = Async.FromContinuations<'T>(fun (completed, caught, _) ->
      lock (this) (fun _ ->
        match this._value, this._exn with
        | Some value, None -> completed value
        | None, Some exn -> caught exn
        | Some value, Some exn ->
          completed value
          caught exn
        | None, None -> ()

        this._completed <- Some completed
        this._caught <- Some caught))
  }

  /// <summary>
  /// Target Async&lt;'T&gt; instance.
  /// </summary>
  member this.Async = this._async

  /// <summary>
  /// Set result value and continue continuation.
  /// </summary>
  /// <param name="value">Result value</param>
  member this.SetResult value =
    lock (this) (fun () ->
      match this._completed with
      | Some completed -> completed value
      | None -> this._value <- Some value)

  /// <summary>
  /// Set exception and continue continuation.
  /// </summary>
  /// <param name="exn">Exception instance</param>
  member this.SetException exn =
    lock (this) (fun () ->
      match this._caught with
      | Some caught -> caught exn
      | None -> this._exn <- Some exn)

///////////////////////////////////////////////////////////////////////////////////

/// <summary>
/// Pseudo lock primitive on F#'s async workflow/.NET Task.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type private AsyncLock () =

  let _queue = new Queue<unit -> unit>()
  let mutable _enter = false

  let locker continuation =
    let result =
      lock (_queue) (fun _ ->
        match _enter with
        | true ->
          _queue.Enqueue(continuation)
          false
        | false ->
          _enter <- true
          true)
    match result with
    | true -> continuation()
    | false -> ()

  let unlocker () =
    let result =
      lock (_queue) (fun _ ->
        match _queue.Count with
        | 0 ->
          _enter <- false
          None
        | _ ->
          Some (_queue.Dequeue()))
    match result with
    | Some continuation -> continuation()
    | None -> ()

  let disposable = {
    new IDisposable with
      member __.Dispose() = unlocker()
  }

  member __.asyncLock() =
    let acs = new AsyncCompletionSource<IDisposable>()
    locker (fun _ -> acs.SetResult disposable)
    acs.Async

///////////////////////////////////////////////////////////////////////////////////

/// <summary>
/// Asynchronos lazy instance generator.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
/// <typeparam name="'T">Computation result type</typeparam>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type internal AsyncLazy<'T> =

  val private _lock : AsyncLock
  val private _asyncBody : unit -> Async<'T>
  val mutable private _value : 'T option

  /// <summary>
  /// Constructor.
  /// </summary>
  /// <param name="asyncBody">Lazy instance factory.</param>
  new (asyncBody: unit -> Async<'T>) = {
    _lock = new AsyncLock()
    _asyncBody = asyncBody
    _value = None
  }

  member internal this.asyncGetValue() = async {
    use! al = this._lock.asyncLock()
    match this._value with
    | Some value -> return value
    | None ->
      let! value = this._asyncBody()
      this._value <- Some value
      return value
  }
