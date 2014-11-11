namespace Persimmon.Output

open System.IO
open System.Xml

type IWritable =
  abstract WriteTo: writer:TextWriter -> unit

module Writable =
  let doNothing =
    { new IWritable with member __.WriteTo(_writer: TextWriter) = () }

  let char (ch: char) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writer.Write(ch)
        }

  let newline =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writer.WriteLine()
        }

  let stringSeq (strs: string seq) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) = 
          strs |> Seq.iter (writer.WriteLine)
        }

  let xdocument (xdoc: XmlDocument) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          xdoc.Save(writer)
        }

  let group (writables: IWritable seq) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writables |> Seq.iter (fun writable -> writable.WriteTo(writer))
        }