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

  let private categories (t: TestMetadata) = Set.ofArray t.Categories

  let make (filter: TestFilter) : TestMetadata -> bool =
    match filter.IncludeCategories.IsEmpty, filter.ExcludeCategories.IsEmpty with
    | true, true -> fun _ -> true
    | false, false -> fun t -> let categories = categories t in (includeCategoryFilter filter.IncludeCategories categories && excludeCategoryFilter filter.ExcludeCategories categories)
    | false, true -> fun t -> let categories = categories t in (includeCategoryFilter filter.IncludeCategories categories)
    | true, false -> fun t -> let categories = categories t in (excludeCategoryFilter filter.ExcludeCategories categories)