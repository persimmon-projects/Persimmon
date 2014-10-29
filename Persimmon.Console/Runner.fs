module Runner

open System
open System.IO
open System.Reflection

open Microsoft.FSharp.Reflection

open Persimmon
open RuntimeUtil

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

let runPersimmonTest (reporter: Reporter) (test: obj) =
  let typeArgs = test.GetType().GetGenericArguments()
  let _, resultField = getTestResultFields typeArgs
  let result = FSharpValue.GetRecordField(test, resultField)
  let case, _ = FSharpValue.GetUnionFields(result, result.GetType())
  let res = (test, typeArgs.[0]) |> RuntimeTestResult.map (fun x -> box ())
  reporter.ReportProgress(res)
  if case.Tag = successCase.Tag then 0 else 1

let typedefis<'T>(typ: Type) =
  typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<'T>

let persimmonTestProps (p: PropertyInfo) = seq {
  let propType = p.PropertyType
  if propType.IsArray then
    if typedefis<TestResult<_>>(propType.GetElementType()) then
      let elemType = propType.GetElementType()
      yield! (p.GetValue(null), elemType) |> RuntimeArray.map box
  elif propType.IsGenericType then
    let genericTypeDef = propType.GetGenericTypeDefinition()
    if genericTypeDef = typedefof<TestResult<_>> then
      yield p.GetValue(null)
    elif genericTypeDef = typedefof<_ seq> && typedefis<TestResult<_>>(propType.GetGenericArguments().[0]) then
      let elemType = propType.GetGenericArguments().[0]
      yield! (p.GetValue(null), elemType) |> RuntimeSeq.map box
    elif genericTypeDef = typedefof<_ list> && typedefis<TestResult<_>>(propType.GetGenericArguments().[0]) then
      let elemType = propType.GetGenericArguments().[0]
      yield! (p.GetValue(null), elemType) |> RuntimeList.map box
}

let persimmonTestMethods (m: MethodInfo) = seq {
  if typedefis<TestResult<_>>(m.ReturnType) then yield m.Invoke(null, [||])
  elif typedefis<_ seq>(m.ReturnType) && typedefis<TestResult<_>>(m.ReturnType.GetGenericArguments().[0]) then
    let elemType = m.ReturnType.GetGenericArguments().[0]
    yield! (m.Invoke(null, [||]), elemType) |> RuntimeSeq.map box
  elif typedefis<_ list>(m.ReturnType) && typedefis<TestResult<_>>(m.ReturnType.GetGenericArguments().[0]) then
    let elemType = m.ReturnType.GetGenericArguments().[0]
    yield! (m.Invoke(null, [||]), elemType) |> RuntimeList.map box
  elif m.ReturnType.IsArray && typedefis<TestResult<_>>(m.ReturnType.GetElementType()) then
    let elemType = m.ReturnType.GetElementType()
    yield! (m.Invoke(null, [||]), elemType) |> RuntimeArray.map box
}

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
