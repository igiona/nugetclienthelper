rmdir /s /q lib
mkdir lib\netstandard2.0
nuget pack CoreLib_0.0.1.nuspec
nuget pack CoreLib_0.0.2.nuspec
nuget pack CoreLib_1.0.0.nuspec
nuget pack TestLib1.nuspec
nuget pack TestLib2.nuspec
nuget pack TestLib3.nuspec