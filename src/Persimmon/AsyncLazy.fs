namespace Microsoft.FSharp.Control

open System
open System.Collections.Generic
open System.Threading

/// <summary>
/// Delegation F#'s async continuation.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
/// <description>
/// Simulate TaskCompletionSource&lt;'T&gt; for F#'s Async&lt;'T&gt;.
/// </description>
/// <typeparam name="'T">Computation result type</typeparam>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type private AsyncCompletionSource<'T>() as this =

  let mutable value : 'T option = None
  let mutable exn : exn option = None
  let mutable completed : ('T -> unit) option = None
  let mutable caught : (exn -> unit) option = None

  let body = Async.FromContinuations<'T>(fun (callback, error, _) ->
    lock (this) (fun _ ->
      match value, exn with
      | Some value, None -> callback value
      | None, Some exn -> error exn
      | Some value, Some exn ->
        callback value
        error exn
      | None, None -> ()

      completed <- Some callback
      caught <- Some error
    )
  )

  /// <summary>
  /// Target Async&lt;'T&gt; instance.
  /// </summary>
  member this.Async = body

  /// <summary>
  /// Set result value and continue continuation.
  /// </summary>
  /// <param name="value">Result value</param>
  member this.Result
    with set(v) =
      lock (this) (fun () ->
        match completed with
        | Some completed -> completed v
        | None -> value <- Some v
      )

  /// <summary>
  /// Set exception and continue continuation.
  /// </summary>
  /// <param name="exn">Exception instance</param>
  member this.Exception
    with set(e) =
      lock (this) (fun () ->
        match caught with
        | Some caught -> caught e
        | None -> exn <- Some e
      )

/// <summary>
/// Pseudo lock primitive on F#'s async workflow/.NET Task.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type private AsyncLock () =

  let queue = new Queue<unit -> unit>()
  let mutable enter = false

  let locker continuation =
    let result =
      lock (queue) (fun _ ->
        match enter with
        | true ->
          queue.Enqueue(continuation)
          false
        | false ->
          enter <- true
          true)
    match result with
    | true -> continuation()
    | false -> ()

  let unlocker () =
    let result =
      lock (queue) (fun _ ->
        match queue.Count with
        | 0 ->
          enter <- false
          None
        | _ ->
          Some (queue.Dequeue()))
    match result with
    | Some continuation -> continuation()
    | None -> ()

  let disposable = {
    new IDisposable with
      member __.Dispose() = unlocker()
  }

  member __.AsyncLock() =
    let acs = new AsyncCompletionSource<IDisposable>()
    locker (fun _ -> acs.Result <- disposable)
    acs.Async

/// <summary>
/// Asynchronos lazy instance generator.
/// (From FSharp.Control.FusionTasks https://github.com/kekyo/FSharp.Control.FusionTasks)
/// </summary>
/// <typeparam name="'T">Computation result type</typeparam>
[<Sealed; NoEquality; NoComparison; AutoSerializable(false)>]
type internal AsyncLazy<'T>(asyncBody: unit -> Async<'T>) =

  let lock = AsyncLock()
  let mutable value : 'T option = None

  member internal this.AsyncGetValue() = async {
    use! al = lock.AsyncLock()
    match value with
    | Some value -> return value
    | None ->
      let! v = asyncBody ()
      value <- Some v
      return v
  }
