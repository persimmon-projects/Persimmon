namespace global

type NonEmptyList<'T> = 'T * 'T list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NonEmptyList =
  let head (xs: NonEmptyList<_>) = let head, _ = xs in head
  let make head tail : NonEmptyList<_> = (head, tail)
  let cons head tail : NonEmptyList<_> = let second, tail = tail in (head, second::tail)
  let singleton head : NonEmptyList<_> = (head, [])
  let append (xs: NonEmptyList<_>) (ys: NonEmptyList<_>) : NonEmptyList<_> =
    let x, xs = xs
    let y, ys = ys
    (x, (xs@(y::ys)))
  let iter action (list: NonEmptyList<'T>) =
    let head, tail = list
    action head
    List.iter action tail
  let toList (list: NonEmptyList<'T>) =
    let head, tail = list
    [ yield head; yield! tail ]

