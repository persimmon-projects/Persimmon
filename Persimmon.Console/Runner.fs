module Runner

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Reflection

open Persimmon
open RuntimeUtil

let passedCase, failedCase, errorCase =
  let typ = typedefof<AssertionResult<_>>
  match typ |> FSharpType.GetUnionCases with
  | [| passed; failed; error |] -> passed, failed, error
  | _ -> failwith "oops!"

let getTestResultFields typeArgs =
  let typ = typedefof<TestResult<_>>
  let typ = typ.MakeGenericType(typeArgs)
  match typ |> FSharpType.GetRecordFields with
  | [| name; parameters; result |] -> name, parameters, result
  | _ -> failwith "oops!"

let runPersimmonTest (reporter: Reporter) (test: obj) =
  let typeArgs = test.GetType().GetGenericArguments()
  let _, _, resultField = getTestResultFields typeArgs
  let result = FSharpValue.GetRecordField(test, resultField)
  let case, _ = FSharpValue.GetUnionFields(result, result.GetType())
  let res = (test, typeArgs.[0]) |> RuntimeTestResult.map (fun x -> box ())
  reporter.ReportProgress(res)
  if case.Tag = passedCase.Tag then 0 else 1

let typedefis<'T>(typ: Type) =
  typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<'T>

let (|Type|_|) (matching: Type) (typ: Type) = if typ = matching then Some typ else None
let (|ArrayType|_|) (typ: Type) = if typ.IsArray then Some (typ.GetElementType()) else None
let (|GenericType|_|) (typ: Type) =
  if typ.IsGenericType then
    Some (typ.GetGenericTypeDefinition(), typ.GetGenericArguments())
  else
    None

let persimmonTests f (typ: Type) = seq {
  let testIF = typeof<ITest>
  match typ with
  | Type testIF _ ->
      yield f ()
  | ArrayType elemType when typedefis<TestResult<_>>(elemType) || elemType = typeof<ITest> ->
      yield! (f (), elemType) |> RuntimeArray.map box
  | GenericType (genTypeDef, _) when genTypeDef = typedefof<TestResult<_>> ->
      yield f ()
  | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ seq> && (typedefis<TestResult<_>>(elemType) || elemType = typeof<ITest>) ->
      yield! (f (), elemType) |> RuntimeSeq.map box
  | GenericType (genTypeDef, [| elemType |]) when genTypeDef = typedefof<_ list> && (typedefis<TestResult<_>>(elemType) || elemType = typeof<ITest>) ->
      yield! (f (), elemType) |> RuntimeList.map box
  | _ -> ()
}

let persimmonTestProps (p: PropertyInfo) = persimmonTests (fun () -> p.GetValue(null)) p.PropertyType
let persimmonTestMethods (m: MethodInfo) = persimmonTests (fun () -> m.Invoke(null, [||])) m.ReturnType

let getPublicTypes (asm: Assembly) =
  asm.GetTypes()
  |> Seq.filter (fun typ -> typ.IsPublic)

let getPublicNestedTypes (typ: Type) =
  typ.GetNestedTypes()
  |> Seq.filter (fun typ -> typ.IsNestedPublic)

let rec runTests' reporter (rcontext: string list) (typ: Type) : int =
  let nestedTestFailures = typ |> getPublicNestedTypes |> Seq.sumBy (runTests' reporter (typ.Name::rcontext))
  let failures =
    seq {
      yield!
        typ.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.collect persimmonTestProps
      yield!
        typ.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Seq.filter (fun m -> not m.IsSpecialName) // ignore getter methods
        |> Seq.filter (fun m -> m.GetParameters() |> Array.isEmpty)
        |> Seq.collect persimmonTestMethods
    }
    |> Seq.sumBy (runPersimmonTest reporter)
  nestedTestFailures + failures

let runTests reporter (typ: Type) = runTests' reporter [] typ

let runAllTests reporter (files: FileInfo list) =
  files
  |> Seq.map (fun file -> Assembly.LoadFrom(file.FullName))
  |> Seq.collect getPublicTypes
  |> Seq.sumBy (runTests reporter)
