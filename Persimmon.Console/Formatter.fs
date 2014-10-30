namespace global

type IWritable =
  abstract WriteLineTo: writer:Writer -> unit
  abstract WriteTo: writer:Writer -> unit

type IFormatter<'T> =
  abstract Format: 'T -> IWritable

module Writables =
  let string str = { new IWritable with
                       member __.WriteLineTo(writer: Writer) = writer.WriteLine(str)
                       member __.WriteTo(writer: Writer) = writer.Write(str) }
  let stringSeq strs = { new IWritable with
                           member __.WriteLineTo(writer: Writer) = strs |> Seq.iter (writer.WriteLine)
                           member __.WriteTo(writer: Writer) = strs |> Seq.iter (writer.Write) }

module Formatters =
  open Persimmon

  module ProgressFormatter =
    let dot =
      { new IFormatter<TestResult<unit>> with
          member __.Format(res: TestResult<unit>) =
            Writables.string begin
              match res.AssertionResult with
              | Passed _ -> "."
              | Failed _ -> "f"
              | Error _ -> "E"
              | Skipped _ -> "s"
            end }

  module SummaryFormatter =
    let normal =
      let toStr (res: TestResult<unit>) =
        seq {
          match res.AssertionResult with
          | Passed _ -> ()
          | Failed errs ->
              yield "Assertion failed: " + res.Name
              yield! errs |> NonEmptyList.toList
          | Error (e, errs) ->
              yield "FATAL ERROR: " + res.Name
              if not (errs.IsEmpty) then
                yield "------------------- finished assertions --------------------"
                yield! errs
              yield "------------------------ exception -------------------------"
              yield e.ToString()
          | Skipped reason ->
              yield "Skipped: " + res.Name
              yield "reason: " + reason
        }

      { new IFormatter<TestResult<unit> seq> with
          member __.Format(xs: TestResult<unit> seq) =
            Writables.stringSeq begin
              xs |> Seq.collect toStr
            end }

  module ErrorFormatter =
    let normal =
      { new IFormatter<string> with
          member __.Format(str: string) = Writables.string str }