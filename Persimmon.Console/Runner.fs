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

let runPersimmonTest (reporter: Reporter) (test: obj) =
  let typeArgs = test.GetType().GetGenericArguments()
  let _, resultField = getTestResultFields typeArgs
  let result = FSharpValue.GetRecordField(test, resultField)
  let case, _ = FSharpValue.GetUnionFields(result, result.GetType())
  let res = mapIgnoreRuntime typeArgs.[0] test // this line means: let res = test |> TestResult.map ignore
  reporter.ReportProgress(res)
  if case.Tag = successCase.Tag then 0 else 1

let persimmonTest (m: MemberInfo) =
  // todo: プロパティやフィールドも拾う？
  // todo: list, seq, arrayも拾う？
  match m with
  | :? MethodInfo as m when m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() = typedefof<TestResult<_>> ->
      Some (m.Invoke(null, [||]))
  | _ -> None

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
    |> Seq.choose persimmonTest
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
