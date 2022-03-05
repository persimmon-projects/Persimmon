### 5.0.1 - March 5 2022
* drop support net45 (support only netstandard2.0) [#141](https://github.com/persimmon-projects/Persimmon/pull/141)
* support F# 5.0 [#141](https://github.com/persimmon-projects/Persimmon/pull/141)
* update tools [#141](https://github.com/persimmon-projects/Persimmon/pull/141)
* add runner APIs [#141](https://github.com/persimmon-projects/Persimmon/pull/141)

### 5.0.0 - Broken packages

### 4.0.2 - July 4 2018
* fix dependency version of Persimmon.Runner

### 4.0.1 - June 20 2018
* add AssemblyInfo [#140](https://github.com/persimmon-projects/Persimmon/pull/140)

### 4.0.0 - May 13 2018
* drop support net20, net35, net40 and pcl259
* fix to ptint help [#131](https://github.com/persimmon-projects/Persimmon/pull/131)
* avoid SerializatioException [#135](https://github.com/persimmon-projects/Persimmon/issues/135)
* support FShstp.Core of different version [#139](https://github.com/persimmon-projects/Persimmon/pull/139)

### 3.1.1 - November 22 2017
* fix top level context name [#130](https://github.com/persimmon-projects/Persimmon/pull/130)

### 3.1.0 - November 17 2017
* implement Equals, GetHashCode and ToString to AssertionResult [#129](https://github.com/persimmon-projects/Persimmon/pull/129)

### 3.0.0 - November 15 2017
* separate AppDomain [#120](https://github.com/persimmon-projects/Persimmon/pull/120)
* apply line number [#122](https://github.com/persimmon-projects/Persimmon/pull/122)
* improve API [#124](https://github.com/persimmon-projects/Persimmon/pull/124)
* fix test suite name [#125](https://github.com/persimmon-projects/Persimmon/pull/125)
* support Category [#126](https://github.com/persimmon-projects/Persimmon/pull/126)

### 2.0.1 - April 6 2017
* fix duplicate collect tests in nested type
* fix get generic arguments
* support F# 4.1
* add line number information [#116](https://github.com/persimmon-projects/Persimmon/pull/116)
* explicit dependency on FSharp.Core nuget package
* fix `use` scope in TestBuilder [#117](https://github.com/persimmon-projects/Persimmon/pull/117), [#118](https://github.com/persimmon-projects/Persimmon/pull/118)
* fix `try-finally` scope in TestBuilder [#119](https://github.com/persimmon-projects/Persimmon/pull/119)
* check type in collect test phase
* dump uncaught error
* fix unique name
* add TestResult#FailureMessages, TestResult#SkipMessages
* fix junit xml report
* improve API(Persimmon.TestResult, Persimmon.Internals.TestRunner)
* fix count errors
* fix lazy evaluation
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
