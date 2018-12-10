//#r @"C:\Users\cernu\Documents\Code\IfCntk\packages\CNTK.CPUOnly\lib\netstandard2.0\Cntk.Core.Managed-2.6.dll"

open System.Reflection
open System.Text.RegularExpressions
open System.IO
open System

let IsWindows =
    System.Runtime.InteropServices.RuntimeInformation.OSDescription.Contains("Windows")

let CntkCoreManagedPath =
    let codebase = Assembly.GetAssembly(typeof<CNTK.CNTKLib>).CodeBase 
    match IsWindows with
    | true -> codebase.Replace("file:///","") 
    | false -> codebase.Replace("file://","") 
    |> Path.GetDirectoryName

let CntkVersion =
    CntkCoreManagedPath
        .Split([|'/';'\\' |])
    |> Array.rev
    |> Array.skipWhile 
         (fun dir -> not <| Regex.IsMatch(dir, @"^\d\.\d(\.\d)?$"))
    |> function
    | [||] -> ""
    | dirs -> dirs |> Array.head
    |> fun str -> str.Replace("\\", "").Replace("/", "");

let PackagesPath =
    CntkCoreManagedPath
    |> Path.GetDirectoryName
    |> fun path -> path.Split([|'/';'\\' |])
    |> Array.rev
    |> Array.skipWhile 
        (fun dir -> not <| dir.ToLower().Contains("cntk."))
    |> Array.skip 1
    |> Array.rev
    |> Array.reduce 
        (fun state current -> sprintf "%s%s%s" state (Path.DirectorySeparatorChar.ToString()) current)

type DllKind = Debug | Release

let ManagedSupportPath kind =
    match kind with
    | Debug -> sprintf "../../support/x64/%s" "Debug"
    | Release -> sprintf "../../support/x64/%s" "Release"
    |> fun supportPath ->  
        Path.Combine(CntkCoreManagedPath,supportPath)
        |> Path.GetFullPath

let DepPaths kind =
    let support = 
        Directory
            .GetDirectories(PackagesPath, "cntk.deps.*")
        |> Array.collect (fun dir -> 
            [|dir;CntkVersion;"support";"x64"|]
            |> Path.Combine
            |> Directory.GetDirectories)
        |> Array.map Path.GetFullPath
    
    let binPath = match kind with Debug -> "Debug" | Release -> "Release"
    let binPaths = 
        support 
        |> Array.map (fun dir -> Path.Combine(dir, binPath))
        |> Array.filter (Directory.Exists)
    support |> Array.append binPaths

let binPaths kind = 
    seq {
        yield CntkCoreManagedPath
        match kind with
        | Debug -> 
            yield Debug |> ManagedSupportPath 
            yield! Debug |> DepPaths
        | Release ->
            yield Release |> ManagedSupportPath 
            yield! Release |> DepPaths
    } 

if "bin" |> Directory.Exists |> not
then 
    Directory.CreateDirectory("bin") |> ignore
else 
    Directory.GetFiles("bin") 
    |> Array.iter(fun file -> printfn "Cleaning file '%s'..." file; FileInfo(file).Delete())
    
binPaths Release
|> Array.ofSeq
|> Array.collect (fun path -> Directory.GetFiles(path, "*.dll"))
|> Array.iter(fun path -> 
        printfn "Copying %s" path
        File.Copy(path, Path.Combine("bin", Path.GetFileName(path))))
        



