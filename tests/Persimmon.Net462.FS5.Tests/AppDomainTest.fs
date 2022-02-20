namespace Persimmon.Tests

open Persimmon
open UseTestNameByReflection

module AppDomainTest =
  open System.Configuration

  let ``should read value from app.config`` = test {
    let actual = ConfigurationManager.AppSettings.["key"]
    do! actual |> assertEquals "value"
  }

  type NonSerializableType(value: int) =
    member __.Value = value

    override this.Equals(other: obj) =
      match other with
      | :? NonSerializableType as other -> this.Value = other.Value
      | _ -> false

    override __.GetHashCode() = 0

    override __.ToString() = "NonSerializableType"
        
  let nonSerializableValue = test {
    return NonSerializableType(3)
  }

  let ``should test non-serializable type`` = test {
    let! actual = nonSerializableValue
    do! actual.Value |> assertEquals 3
  }