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

let runPersimmonTest (output: Writer, error: Writer) (test: obj) =
  let typeArgs = test.GetType().GetGenericArguments()
  let nameField, resultField = getTestResultFields typeArgs
  let result = FSharpValue.GetRecordField(test, resultField)
  let case, value = FSharpValue.GetUnionFields(result, result.GetType())
  if case.Tag = successCase.Tag then
    0
  else
    let name = FSharpValue.GetRecordField(test, nameField)
    output.WriteLine("Assertion failed: " + string name)
    let errs = value.[0] :?> NonEmptyList<string>
    errs |> NonEmptyList.iter (output.WriteLine)
    1

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

let rec runTests' (output: Writer, error: Writer) (rcontext: string list) (typ: Type) : int =
  let nestedTestResults = typ |> getPublicNestedTypes |> Seq.sumBy (runTests' (output, error) (typ.Name::rcontext))
  let results =
    typ.GetMembers()
    |> Seq.choose persimmonTest
    |> Seq.sumBy (runPersimmonTest (output, error))
  nestedTestResults + results

let runTests (output: Writer, error: Writer) (typ: Type) =
  let nestedTestResults = typ |> getPublicNestedTypes |> Seq.sumBy (runTests' (output, error) [typ.FullName])
  let results = runTests' (output, error) [] typ
  nestedTestResults + results

let runAllTests (output: Writer, error: Writer) (files: FileInfo list) =
  files
  |> Seq.map (fun file -> Assembly.LoadFrom(file.FullName))
  |> Seq.collect getPublicTypes
  |> Seq.sumBy (runTests (output, error))
