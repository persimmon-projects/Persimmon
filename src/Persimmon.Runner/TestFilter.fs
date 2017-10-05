namespace Persimmon.Runner

type TestFilter = {
  IncludeCategories: Set<string>
  ExcludeCategories: Set<string>
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TestFilter =
  open Persimmon

  let allPass = { IncludeCategories = Set.empty; ExcludeCategories = Set.empty }

  let private excludeCategoryFilter (excludes: Set<string>) (categories: Set<string>) =
    Set.intersect excludes categories |> Set.isEmpty

  let private includeCategoryFilter (includes: Set<string>) (categories: Set<string>) =
    Set.intersect includes categories = includes

  let private makeSet (t: TestMetadata) = Set.ofArray t.Categories

  let private testCaseFilter (pred: TestCase -> bool) = fun (t: TestMetadata) ->
    match t with
    | :? TestCase as tc -> pred tc
    | _ -> true

  let private (|Empty|NotEmpty|) (x: Set<_>) = if x.IsEmpty then Empty else NotEmpty

  let make (filter: TestFilter) : TestMetadata -> bool =
    match filter.IncludeCategories, filter.ExcludeCategories with
    | Empty, Empty -> fun _ -> true
    | NotEmpty, NotEmpty ->
      testCaseFilter <| fun tc -> let categories = makeSet tc in (includeCategoryFilter filter.IncludeCategories categories && excludeCategoryFilter filter.ExcludeCategories categories)
    | NotEmpty, Empty ->
      testCaseFilter <| fun tc -> let categories = makeSet tc in (includeCategoryFilter filter.IncludeCategories categories)
    | Empty, NotEmpty ->
      testCaseFilter <| fun tc -> let categories = makeSet tc in (excludeCategoryFilter filter.ExcludeCategories categories)