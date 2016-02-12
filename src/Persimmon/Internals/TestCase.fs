namespace Persimmon.Internals

open System

[<Sealed>]
[<Serializable>]
type TestCase(fullyQualifiedName: string, className: string, source: string) =
  let mutable fullyQualifiedName = fullyQualifiedName
  let mutable source = source
  let mutable displayName = ""
  member __.FullyQualifiedName with get() = fullyQualifiedName and set(value) = fullyQualifiedName <- value
  member __.ClassName = className
  member __.Source with get() = source and set(value) = source <- value
  member __.DisplayName with get() = displayName and set(value) = displayName <- value

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal TestCase =

  open Persimmon.ActivePatterns

  let rec ofTestObject source (t: Type, o) =
    let name = t.FullName
    match o with
    | Context ctx ->
      TestCase(ctx.Name, name, source, DisplayName = ctx.Name)
    | TestCase case ->
      TestCase(case.FullName, name, source, DisplayName = case.FullName)
