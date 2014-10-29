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

module Runtime =
  let invoke (m: MethodInfo) typeArgs args =
    let f =
      match typeArgs with
      | [||] -> m
      | _ -> m.MakeGenericMethod(typeArgs)
    f.Invoke(null, args)

  let lambda (srcType, dstType) body =
    let typ = FSharpType.MakeFunctionType(srcType, dstType)
    FSharpValue.MakeFunction(typ, body)

module RuntimeSeq =
  let private typ = (typeof<_ list>).Assembly.GetType("Microsoft.FSharp.Collections.SeqModule")

  let private mapMethod = typ.GetMethod("Map")
  let map<'TDest> (f: obj -> obj (* 'a -> 'TDest *)) (xs: obj (* 'a seq *), elemType: Type (* typeof<'a> *)) =
    let f = Runtime.lambda (elemType, typeof<'TDest>) f
    let result = Runtime.invoke mapMethod [| elemType; typeof<'TDest> |] [| f; xs |]
    result :?> 'TDest seq

module RuntimeList =
  let private typ = (typeof<_ list>).Assembly.GetType("Microsoft.FSharp.Collections.ListModule")

  let private mapMethod = typ.GetMethod("Map")
  let map<'TDest> (f: obj -> obj (* 'a -> 'TDest *)) (xs: obj (* 'a list *), elemType: Type (* typeof<'a>*)) =
    let f = Runtime.lambda (elemType, typeof<'TDest>) f
    let result = Runtime.invoke mapMethod [| elemType; typeof<'TDest> |] [| f; xs |]
    result :?> 'TDest list

module RuntimeArray =
  let private typ = (typeof<_ list>).Assembly.GetType("Microsoft.FSharp.Collections.ArrayModule")

  let private mapMethod = typ.GetMethod("Map")
  let map<'TDest> (f: obj -> obj (* 'a -> 'TDest *)) (xs: obj (* 'a[] *), elemType: Type (* typeof<'a>*)) =
    let f = Runtime.lambda (elemType, typeof<'TDest>) f
    let result = Runtime.invoke mapMethod [| elemType; typeof<'TDest> |] [| f; xs |]
    result :?> 'TDest[]

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

let persimmonTestProps (p: PropertyInfo) = seq {
  let propType = p.PropertyType
  if propType.IsArray then
    if propType.GetElementType() = typeof<TestResult<unit>> then
      yield! (p.GetValue(null), typeof<TestResult<unit>>) |> RuntimeArray.map box
  elif propType.IsGenericType then
    let genericTypeDef = propType.GetGenericTypeDefinition()
    if genericTypeDef = typedefof<TestResult<_>> then
      yield p.GetValue(null)
    elif genericTypeDef = typedefof<_ seq> && propType.GetGenericArguments().[0] = typeof<TestResult<unit>> then
      yield! (p.GetValue(null), typeof<TestResult<unit>>) |> RuntimeSeq.map box
    elif genericTypeDef = typedefof<_ list> && propType.GetGenericArguments().[0] = typeof<TestResult<unit>> then
      yield! (p.GetValue(null), typeof<TestResult<unit>>) |> RuntimeList.map box
}

let persimmonTestMethods (m: MethodInfo) = seq {
  if m |> returnTypeIs<TestResult<_>> then yield m.Invoke(null, [||])
  elif m |> returnTypeIs<_ seq> && m.ReturnType.GetGenericArguments().[0] = typeof<TestResult<unit>> then
    yield! (m.Invoke(null, [||]), typeof<TestResult<unit>>) |> RuntimeSeq.map box
  elif m |> returnTypeIs<_ list> && m.ReturnType.GetGenericArguments().[0] = typeof<TestResult<unit>> then
    yield! (m.Invoke(null, [||]), typeof<TestResult<unit>>) |> RuntimeList.map box
  elif m.ReturnType.IsArray && m.ReturnType.GetElementType() = typeof<TestResult<unit>> then
    yield! (m.Invoke(null, [||]), typeof<TestResult<unit>>) |> RuntimeArray.map box
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
