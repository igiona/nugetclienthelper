rmdir /s /q lib
mkdir lib\netstandard2.0
nuget pack CoreLib_0.0.1.nuspec
nuget pack CoreLib_0.0.2.nuspec
nuget pack CoreLib_1.0.0.nuspec
nuget pack TestLib1.nuspec
nuget pack TestLib2.nuspec
nuget pack TestLib3.nuspec

rmdir /s /q lib
mkdir lib\net45
nuget pack TestVersionConflict_1.0.0.nuspec

nuget pack TestFrameworkAny_1.0.0.nuspec

rmdir /s /q lib
mkdir lib
nuget pack TestFrameworkAny_2.0.0.nuspec
mkdir lib\Any
nuget pack TestFrameworkAny_3.0.0.nuspec
