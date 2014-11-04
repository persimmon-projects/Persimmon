module Persimmon

type ReturnType = UnitType | ValueType

type AssertionResult<'T> =
  | Passed of 'T
  | Failed of NonEmptyList<string>
  | Error of exn * string list

module AssertionResult =
  let map f = function
  | Passed s -> Passed (f s)
  | Failed errs -> Failed errs
  | Error (e, errs) -> Error (e, errs)

// hack: F# doesn't support the GADTs. So we encode it using the class hierarchy.
//              +-----+
//              |ITest|<--------+
//              +-----+         |
//                △            |
//                ｜            |
//         +------------+       |
//         |            |       |
// +--------------+ +-------+   |
// |TestResult<'T>| |Context|◇-+
// +--------------+ +-------+
type ITest = interface end

type Context = {
  Name: string
  Children: ITest list
}
with
  interface ITest

type BoxedTestResult = {
  Name: string
  Parameters: obj list
  AssertionResult: AssertionResult<obj>
}
with
  member this.FullName =
    if this.Parameters.IsEmpty then this.Name
    else this.Name + "(" + (String.concat ", " (this.Parameters |> List.map string)) + ")"

type TestResult<'T> = {
  Name: string
  Parameters: obj list
  AssertionResult: AssertionResult<'T>
}
with
  member this.FullName =
    if this.Parameters.IsEmpty then this.Name
    else this.Name + "(" + (String.concat ", " (this.Parameters |> List.map string)) + ")"

  member this.BoxTypeParam () =
    { BoxedTestResult.Name = this.Name
      Parameters = this.Parameters
      AssertionResult = this.AssertionResult |> AssertionResult.map box }

  interface ITest

module TestExtension =
  let conv (x: obj) =
    let typ = x.GetType()
    let m = typ.GetMethod("BoxTypeParam")
    m.Invoke(x, [||]) :?> BoxedTestResult

  type ITest with
    member this.Match<'T>(f, g) : 'T =
      match this with
      | :? Context as c -> f c
      | self when self.GetType().GetGenericTypeDefinition() = typedefof<TestResult<_>> ->
          g (conv self)
      | _other ->
          failwithf "oops!: %A" (typeof<'T>)

open TestExtension

let (|Context|TestResult|) (test: ITest) =
  test.Match((fun c -> Context c), (fun tr -> TestResult tr))

let context name children =
  { Name = name; Children = children } :> ITest

module TestResult =
  let map (f: 'a -> 'b) (x: TestResult<'a>) =
    let mapped = x.AssertionResult |> AssertionResult.map f
    { Name = x.Name; Parameters = x.Parameters; AssertionResult = mapped } :> ITest

type TestBuilder(description: string) =
  member __.Return(x) = Passed x
  member __.ReturnFrom(x, _) = x
  member __.Source(x: AssertionResult<unit>) = (x, UnitType)
  member __.Source(x: AssertionResult<_>) = (x, ValueType)
  member __.Source(x: TestResult<unit>) = (x.AssertionResult, UnitType)
  member __.Source(x: TestResult<_>) = (x.AssertionResult, ValueType)
  member __.Bind(x, f: 'T -> AssertionResult<_>) =
    match x with
    | (Passed x, _) -> f x
    | (Failed errs1, UnitType) ->
      assert (typeof<'T> = typeof<unit>) // runtime type is unit. So Unchecked.defaultof<'T> is not used inner f.
      try
        match f (Unchecked.defaultof<'T>) with
        | Passed _ -> Failed errs1
        | Failed errs2 -> Failed (NonEmptyList.append errs1 errs2)
        | Error (e, errs2) -> Error (e, List.append (errs1 |> NonEmptyList.toList) errs2)
      with
        e -> Error (e, errs1 |> NonEmptyList.toList)
    | (Failed xs, ValueType) -> Failed xs
    | (Error (e, errs), _) -> Error (e, errs)
  member __.Delay(f: unit -> AssertionResult<_>) = f
  member __.Run(f) =
    { Name = description; Parameters = []; AssertionResult = try f () with e -> Error (e, []) }

let test description = TestBuilder(description)

type TrapBuilder () =
  member __.Zero () = ()
  member __.Delay(f: unit -> _) = f
  member __.Run(f) =
    try
      f () |> ignore
      Failed (NonEmptyList.singleton "Expect thrown exn but not")
    with
      e -> Passed e

let trap = TrapBuilder ()
 
let inline checkWith returnValue expected actual =
  if expected = actual then Passed returnValue
  else Failed (NonEmptyList.singleton (sprintf "Expect: %A\nActual: %A" expected actual))

let fail msg = Failed (NonEmptyList.singleton msg)
let pass v = Passed v

let check expected actual = checkWith actual expected actual
let assertEquals expected actual = checkWith () expected actual

type Append =
  | Append
  static member (?<-) (_: unit seq, Append, _: 'a seq) = fun (y: 'a) -> Seq.singleton y
  static member (?<-) (xs: ('a * 'b) seq, Append, _: ('a * 'b) seq) = fun (y: 'a * 'b) -> seq { yield! xs; yield y }

let inline append xs ys =
  (xs ? (Append) <- Seq.empty) ys

type ToList =
  | ToList
  static member (?<-) ((a1: 'a1, a2: 'a2), ToList, _: obj list) = fun () -> [ box a1; box a2 ]
  static member (?<-) ((a1: 'a1, a2: 'a2, a3: 'a3), ToList, _: obj list) = fun () -> [ box a1; box a2; box a3 ]

let inline toList x =
  (x ? (ToList) <- []) ()

type ParameterizeBuilder() =
  member __.Delay(f: unit -> _) = f
  member __.Run(f) = f ()
  member __.Yield(x) = Seq.singleton x
  member __.YieldFrom(xs: _ seq) = xs
  member __.For(source : _ seq, body : _ -> _ seq) = source |> Seq.collect body
  [<CustomOperation("case")>]
  member inline __.Case(source, case) = append source case
  [<CustomOperation("run")>]
  member inline __.RunTests(source: _ seq, f: _ -> TestResult<_>) =
    source
    |> Seq.map (fun x -> let ret = f x in { ret with Parameters = toList x } :> ITest)
  [<CustomOperation("source")>]
  member __.Source (_, source: seq<_>) = source

let parameterize = ParameterizeBuilder()
