(*** hide ***)
// このコードブロックは生成された HTML ドキュメントでは省略されます。ドキュメントで
// 見せたくない補助的なものを定義するために使います。
#I "../../../bin/Persimmon/netstandard2.0"
#I "../../../bin/Persimmon.Runner/netstandard2.0"
#r "Persimmon.dll"
open Persimmon

type Provider() =
  member __.getSomeData(_) = ""

let getProvider _ = Provider()

(**
<div class="blog-post">

# 特徴

## コンピュテーション式の活用

Persimmon はいくつかの機能にコンピュテーション式を使っています。

* テストの記述
* パラメータ化されたテストの記述
* 期待された例外を明示

コンピュテーション式を用いることで、既存の F# のためのテスティングフレームワークよりも簡潔に、柔軟に、安全にテストを書けます。

*)

let f n =
  if n > 0 then n * 2
  else failwith "oops!"

let tests = [
  test "(f 21) should be equal to 42" {
    do! assertEquals 42 (f 21)
  }
  test "(f 0) should raise exn" {
    let! e = trap { it (f 0) }
    do! assertEquals (typeof<exn>) (e.GetType())
  }
]

(**

## テストのマーク

多くのテスティングフレームワークでは、属性か命名規則を使ってテストをマークします。
しかし、 Persimmon はこれらを使ってテストをマークしません。

Persimmon は ``TestObject`` かその派生型を返す変数、プロパティ、メソッド（``unit`` 引数のみ）をマークします。

## 合成可能なテスト

Persimmon のテストは合成可能です。

Persimmon のテストはいくつかのテストを受け取って新しいテストを返せます。

*)

let getSomeData x y = test "" {
  let provider = getProvider x
  let res = provider.getSomeData y
  do! assertNotEquals "" res
  return res
}

let composedTests = [
  test "test1" {
    let! data = getSomeData [(10, "ten"); (3, "three")] 3
    do! assertEquals "three" data
  }
  test "test2" {
    let! data = getSomeData [(10, "ten"); (3, "three")] 10
    do! assertEquals "ten" data
  }
]

(**

## 継続可能なアサーション

（結果を持たない） Persimmon のアサーションは、そのアサーションが違反したとしても残りのアサーションの実行を続けます。
また Persimmon はテスト内のすべての違反したアサーションを列挙できます。
この振る舞いは情報量を犠牲にせずにテストの記述を簡略化できるというという利点をもたらします。

*)

// このテストは2つのアサーション違反をレポートします。
let ``some assertions test`` = [
  test "test1" {
    do! assertEquals 1 2 // 実行し違反する
    do! assertEquals 2 2 // 実行を続けパスする
    do! assertEquals 3 4 // 実行を続け違反する
  }
]

(**

## テストのカテゴリ化

テストにカテゴリを設定すると、実行するテストをカテゴリでフィルタできます。

*)

let categorizedTest =
  test "test1" {
    do! assertEquals 2 1
  }
  |> category "SomeCategory" // "SomeCategory" が付与されます

// このモジュール配下の全てのテストに "SomeCategory" が付与されます
[<Category("SomeCategory")>]
module CategorizedTests =
  let someTest =
    test "test2" {
      do! assertEquals 2 1
    }

(**

</div>
*)
