namespace Persimmon.Output

open System.IO
open System

type PrinterInfo<'T> = { Writer: TextWriter; Formatter: IFormatter<'T> }

type Printer<'T> (infos: PrinterInfo<'T> list) =
  new (writer: TextWriter, formatter: IFormatter<'T>) =
    new Printer<'T>([ { Writer = writer; Formatter = formatter }])

  member __.Print(x: 'T) =
    infos |> List.iter (fun { Writer = w; Formatter = f } -> f.Format(x).WriteTo(w))

  member this.Dispose() = (this :> IDisposable).Dispose()
  interface IDisposable with
    member __.Dispose() = 
      infos |> List.iter (fun info -> info.Writer.Dispose())