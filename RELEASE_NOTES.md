### 2.0.1-beta1 - April 3 2017
* support F# 4.1
* add line number information [#116](https://github.com/persimmon-projects/Persimmon/pull/116)
* explicit dependency on FSharp.Core nuget package
* fix `use` scope in TestBuilder [#117](https://github.com/persimmon-projects/Persimmon/pull/117), [#118](https://github.com/persimmon-projects/Persimmon/pull/118)
* fix `try-finally` scope in TestBuilder [#119](https://github.com/persimmon-projects/Persimmon/pull/119)

### 2.0.1-alpha6 - November 24 2016
* check type in collect test phase
* dump uncaught error

### 2.0.1-alpha5 - November 21 2016
* fix unique name

### 2.0.1-alpha4 - November 12 2016
* add TestResult#FailureMessages, TestResult#SkipMessages
* fix junit xml report
* improve API(Persimmon.TestResult, Persimmon.Internals.TestRunner)

### 2.0.1-alpha3 - November 3 2016
* fix count errors
* fix lazy evaluation

### 2.0.1-alpha2 - November 1 2016
* support Visual Studio test explorer [#111](https://github.com/persimmon-projects/Persimmon/pull/111)
* support Core clr [#114](https://github.com/persimmon-projects/Persimmon/pull/114)
* drop support Persimmon.Script [#115](https://github.com/persimmon-projects/Persimmon/pull/115)

### 2.0.0 - Broken packages

### 1.2.0 - September 4 2016
* fix copy exceptions [#113](https://github.com/persimmon-projects/Persimmon/pull/113)

### 1.1.0 - April 25 2016
* add test suite summary for XML output [#102](https://github.com/persimmon-projects/Persimmon/pull/102)
* fix evaluation count [#104](https://github.com/persimmon-projects/Persimmon/pull/104)
* add `try finally` keyword for `test` computation expression [#109](https://github.com/persimmon-projects/Persimmon/pull/109)
* avoid to crash console when source raised exception in `parameterize` [#110](https://github.com/persimmon-projects/Persimmon/pull/110)

### 1.0.2 - February 7 2016
* show top level type name [#98](https://github.com/persimmon-projects/Persimmon/pull/98)
* output summary with multiple output forms [#99](https://github.com/persimmon-projects/Persimmon/pull/99)
* fix list pretty printer [#100](https://github.com/persimmon-projects/Persimmon/pull/100)

### 1.0.1 - November 29 2015
* return error if trap builder catch other exn

### 1.0.0 - October 18 2015
* initial release
