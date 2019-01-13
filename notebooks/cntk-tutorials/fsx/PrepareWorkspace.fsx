open System.Reflection
open System.Text.RegularExpressions
open System.IO
open System

(* If run outside notebook, requires 
   CNTK to have been referenced 
   
   i.e. #load "../.paket/load/CNTK.CPUOnly.fsx"
*)

type DllKind =
    | Debug
    | Release

type Processor =
    | CPU
    | GPU

let CpuOnlyPackages =
    [ "CNTK.CPUOnly"; "CNTK.Deps.MKL"; "CNTK.Deps.OpenCV.Zip" ]
    |> List.map (fun str -> str.ToLower())
    |> Set.ofList

let GpuPackages =
    [ "CNTK.GPU"; "CNTK.Deps.Cuda"; "CNTK.Deps.cuDNN"; "CNTK.Deps.MKL";
      "CNTK.Deps.OpenCV.Zip " ]
    |> List.map (fun str -> str.ToLower())
    |> Set.ofList

let ProcFilter proc (path : string) =
    let loCase = path.ToLower().Split([| '\\'; '/' |]) |> Set.ofArray
    match proc with
    | CPU ->
        CpuOnlyPackages
        |> Set.intersect loCase
        |> Set.count
        |> (<) 0
    | GPU ->
        GpuPackages
        |> Set.intersect loCase
        |> Set.count
        |> (<) 0

let IsWindows =
    System.Runtime.InteropServices.RuntimeInformation.OSDescription.Contains
        ("Windows")

let CntkCoreManagedPath =
    let codebase = Assembly.GetAssembly(typeof<CNTK.CNTKLib>).CodeBase
    match IsWindows with
    | true -> codebase.Replace("file:///", "")
    | false -> codebase.Replace("file://", "")
    |> Path.GetDirectoryName

let CntkVersion =
    CntkCoreManagedPath.Split([| '/'; '\\' |])
    |> Array.rev
    |> Array.skipWhile
           (fun dir -> not <| Regex.IsMatch(dir, @"^\d\.\d(\.\d)?$"))
    |> function
    | [||] -> ""
    | dirs -> dirs |> Array.head
    |> fun str -> str.Replace("\\", "").Replace("/", "")

let CntkProc =
    if Regex.IsMatch
           (CntkCoreManagedPath, @"[\\/]cntk\.cpuonly[\\/]",
            RegexOptions.IgnoreCase) then CPU
    else GPU

let PackagesPath =
    CntkCoreManagedPath
    |> Path.GetDirectoryName
    |> fun path -> path.Split([| '/'; '\\' |])
    |> Array.rev
    |> Array.skipWhile (fun dir -> not <| dir.ToLower().Contains("cntk."))
    |> Array.skip 1
    |> Array.rev
    |> Array.reduce
           (fun state current ->
           sprintf "%s%s%s" state (Path.DirectorySeparatorChar.ToString())
               current)

let ManagedSupportPath kind =
    match kind with
    | Debug -> sprintf "../../support/x64/%s" "Debug"
    | Release -> sprintf "../../support/x64/%s" "Release"
    |> fun supportPath ->
        Path.Combine(CntkCoreManagedPath, supportPath) |> Path.GetFullPath

let DepPaths proc kind =
    let support =
        Directory.GetDirectories(PackagesPath, "cntk.deps.*")
        |> Array.filter (ProcFilter proc)
        |> Array.collect (fun dir ->
               [| dir; CntkVersion; "support"; "x64" |]
               |> Path.Combine
               |> Directory.GetDirectories)
        |> Array.map Path.GetFullPath

    let binPath =
        match kind with
        | Debug -> "Debug"
        | Release -> "Release"

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
            yield! Debug |> DepPaths CntkProc
        | Release ->
            yield Release |> ManagedSupportPath
            yield! Release |> DepPaths CntkProc        
    }

let CreateOrCleanLocalBinFolder folderName =
    if folderName
       |> Directory.Exists
       |> not
    then Directory.CreateDirectory(folderName) |> ignore
    else
        Directory.GetFiles(folderName)
        |> Array.iter (fun file ->
               printfn "Removing file '%s'..." file
               FileInfo(file).Delete())

let CopyDependenciesToLocalFolder binFolder dependencyKind =
    binPaths dependencyKind
    |> Array.ofSeq
    |> Array.collect (fun path -> Directory.GetFiles(path, "*.dll"))
    |> Array.map (fun path ->
           let packageIndex = path.IndexOf("package")
           printfn "Copying from '%s'" <| path.Substring(packageIndex)
           File.Copy(path, Path.Combine(binFolder, Path.GetFileName(path)))
           FileInfo(path).Length)
    |> Array.sum
    |> fun sum -> printfn "Copied %.02fMB" (float (sum / 1024L / 1024L))

CreateOrCleanLocalBinFolder "bin"
CopyDependenciesToLocalFolder "bin" Release