namespace global

open System

type Printer<'T> (writer: Writer, formatter: IFormatter<'T>) =
  member __.Print (x: 'T) =
    formatter.Format(x).WriteTo(writer)

  member __.PrintLine (x: 'T) =
    formatter.Format(x).WriteLineTo(writer)

  member this.Dispose () = (this :> IDisposable).Dispose()
  interface IDisposable with
    member __.Dispose () =
      writer.Dispose()
