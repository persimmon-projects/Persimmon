namespace Persimmon.Output

open System.IO
open System.Xml.Linq

open Persimmon.Internals

/// This interface represents something that can write to TextWriter.
type IWritable =
  abstract WriteTo: writer:TextWriter -> unit

/// Some implementations of the IWritable.
module Writable =
  /// This does not write anything to TextWriter.
  let doNothing =
    { new IWritable with member __.WriteTo(_writer: TextWriter) = () }

  /// Create IWritable that writes one character to TextWriter.
  let char (ch: char) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writer.Write(ch)
        }

  /// Create IWritable that writes newline to TextWriter.
  let newline =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writer.WriteLine()
        }

  /// Create IWritable that writes lines to TextWriter. 
  let stringSeq (strs: string seq) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) = 
          strs |> Seq.iter (writer.WriteLine)
        }

  /// Create IWritable that writes xml to TextWriter.
  let xdocument (xdoc: XDocument) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          xdoc.Save(writer)
        }

  /// Create IWritable that is a grouping of multiple IWritable.
  let group (writables: IWritable seq) =
    { new IWritable with
        member __.WriteTo(writer: TextWriter) =
          writables |> Seq.iter (fun writable -> writable.WriteTo(writer))
        }