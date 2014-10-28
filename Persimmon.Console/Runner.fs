module Runner

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Reflection

open Persimmon

let successCase, failureCase =
  let typ = typedefof<AssertionResult<_>>
  match typ |> FSharpType.GetUnionCases with
  | [| success; failure |] -> success, failure
  | _ -> failwith "oops!"

let getTestResultFields typeArgs =
  let typ = typedefof<TestResult<_>>
  let typ = typ.MakeGenericType(typeArgs)
  match typ |> FSharpType.GetRecordFields with
  | [| name; result |] -> name, result
  | _ -> failwith "oops!"

let ignoreRuntime (typArg: Type) =
  let ignoreType = FSharpType.MakeFunctionType(typArg, typeof<unit>)
  FSharpValue.MakeFunction(ignoreType, fun _ -> box ())

let mapIgnoreRuntime (typArg: Type) (res: obj) : TestResult<unit> =
  let typ = (typeof<TestResult<_>>).Assembly.GetType("Persimmon+TestResult")
  let map = typ.GetMethod("map").MakeGenericMethod([| typArg; typeof<unit> |])
  let result = map.Invoke(null, [| ignoreRuntime typArg; res |])
  result :?> TestResult<unit>

let boxRuntime =
  let boxType = FSharpType.MakeFunctionType(typeof<TestResult<unit>>, typeof<obj>)
  FSharpValue.MakeFunction(boxType, id)

let mapBoxRuntime (res: obj) : obj list =
  let typ = (typeof<_ list>).Assembly.GetType("Microsoft.FSharp.Collections.ListModule")
  let map = typ.GetMethod("Map").MakeGenericMethod([| typeof<TestResult<unit>>; typeof<obj> |])
  let result = map.Invoke(null, [| boxRuntime; res |]) // this line means: let result = res |> List.map box
  result :?> obj list

let runPersimmonTest (reporter: Reporter) (test: obj) =
  let typeArgs = test.GetType().GetGenericArguments()
  let _, resultField = getTestResultFields typeArgs
  let result = FSharpValue.GetRecordField(test, resultField)
  let case, _ = FSharpValue.GetUnionFields(result, result.GetType())
  let res = mapIgnoreRuntime typeArgs.[0] test // this line means: let res = test |> TestResult.map ignore
  reporter.ReportProgress(res)
  if case.Tag = successCase.Tag then 0 else 1

let returnTypeIs<'T>(m: MethodInfo) =
  m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() = typedefof<'T>

let persimmonTest (m: MemberInfo) =
  match m with
  | :? MethodInfo as m when m |> returnTypeIs<TestResult<_>> ->
      [ m.Invoke(null, [||]) ]
  | :? MethodInfo as m when m |> returnTypeIs<list<_>> && m.ReturnType.GetGenericArguments().[0] = typeof<TestResult<unit>> ->
      m.Invoke(null, [||]) |> mapBoxRuntime
  | _ -> []

let getPublicTypes (asm: Assembly) =
  asm.GetTypes()
  |> Seq.filter (fun typ -> typ.IsPublic)

let getPublicNestedTypes (typ: Type) =
  typ.GetNestedTypes()
  |> Seq.filter (fun typ -> typ.IsPublic)

let rec runTests' reporter (rcontext: string list) (typ: Type) : int =
  let nestedTestResults = typ |> getPublicNestedTypes |> Seq.sumBy (runTests' reporter (typ.Name::rcontext))
  let results =
    typ.GetMembers()
    |> Seq.collect persimmonTest
    |> Seq.sumBy (runPersimmonTest reporter)
  nestedTestResults + results

let runTests reporter (typ: Type) =
  let nestedTestResults = typ |> getPublicNestedTypes |> Seq.sumBy (runTests' reporter [typ.FullName])
  let results = runTests' reporter [] typ
  nestedTestResults + results

let runAllTests reporter (files: FileInfo list) =
  files
  |> Seq.map (fun file -> Assembly.LoadFrom(file.FullName))
  |> Seq.collect getPublicTypes
  |> Seq.sumBy (runTests reporter)
