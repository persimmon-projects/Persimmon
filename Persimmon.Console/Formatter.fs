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
  open Persimmon.TestExtension

  module ProgressFormatter =
    let dot =
      { new IFormatter<ITest> with
          member __.Format(test: ITest) =
            Writables.string begin
              match test with
              | Context _ -> ""
              | TestResult res ->
                  match res.AssertionResult with
                  | Passed _ -> "."
                  | Failed _ -> "f"
                  | Error _ -> "E"
            end }

  module SummaryFormatter =
    let private bar width (barChar: char) (center: string) =
      let barLen = width - center.Length - 2
      let left = barLen / 2
      let res = (String.replicate left (string barChar)) + " " + center + " "
      let right = width - res.Length
      res + (String.replicate right (string barChar))

    let normal =
      let toStr = function
      | Context context ->
          Seq.singleton (bar 70 '=' context.Name)
      | TestResult res ->
          seq {
            match res.AssertionResult with
            | Passed _ -> ()
            | Failed errs ->
                yield "Assertion failed: " + res.FullName
                yield! errs |> NonEmptyList.toList
            | Error (e, errs) ->
                yield "FATAL ERROR: " + res.FullName
                if not (errs.IsEmpty) then
                  yield bar 70 '-' "finished assertions"
                  yield! errs
                yield bar 70 '-' "exception"
                yield e.ToString()
          }

      { new IFormatter<ITest seq> with
          member __.Format(xs: ITest seq) =
            Writables.stringSeq begin
              xs |> Seq.collect toStr
            end }

  module ErrorFormatter =
    let normal =
      { new IFormatter<string> with
          member __.Format(str: string) = Writables.string str }