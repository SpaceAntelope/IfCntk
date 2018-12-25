let cellTemplate kind (content : string) =
    let template = """{
        "cell_type": "##kind##",
        "metadata": {},
        "source": [
            "##content##"
        ]
        ##code-props##
    }"""
    template.Replace("##content##",
                     content.Replace("\n", "\\n")
                            .Replace("\"", "\\\"").Trim())
            .Replace("##kind##", kind).Replace("##code-props##",
                                               match kind with
                                               | "code" ->
                                                   ""","execution_count": null,"outputs": []"""
                                               | _ -> "")

let container cells =
    let template = """
 {
 "cells": [##cells##],
 "metadata": {
  "kernelspec": {
   "display_name": "F#",
   "language": "fsharp",
   "name": "ifsharp"
  },
  "language": "fsharp",
  "language_info": {
   "codemirror_mode": "",
   "file_extension": ".fs",
   "mimetype": "text/x-fsharp",
   "name": "fsharp",
   "nbconvert_exporter": "",
   "pygments_lexer": "",
   "version": "4.3.1.0"
  }
 },
 "nbformat": 4,
 "nbformat_minor": 2
}
"""
    template.Replace("##cells##", cells)

open System
open System.IO
open System.Text.RegularExpressions
open System.Net

let txt =
    Path.Combine(__SOURCE_DIRECTORY__, "deedle.tutorial.fsx")
    |> File.ReadAllText

type Token =
    | Markdown of string
    | Code of string
    | Other of string

let (|IsMarkdown|_|) (str : string) =
    if str <> String.Empty then
        let m =
            Regex.Match
                (str, @"^\s*\(\*\*.*?(?=\*\))\*\)", RegexOptions.Singleline)
        if m.Success && m.Value <> String.Empty then
            Some(m.Value, str.Replace(m.Value, ""))
        else None
    else None

let (|IsCode|_|) (str : string) =
    if str <> String.Empty then
        let m = Regex.Match(str, @".*?(?=\(\*)", RegexOptions.Singleline)
        if m.Success && m.Value <> String.Empty then
            Some(m.Value, str.Replace(m.Value, ""))
        else None
    else None

let rec tokenize (str : string) =
    seq {
        match str with
        | "" -> yield Other str
        | IsMarkdown(m, rest) ->
            yield Markdown m
            yield! tokenize rest
        | IsCode(m, rest) ->
            yield Code m
            yield! tokenize rest
        | _ -> yield Other str
    }

let webclient = new WebClient()
let baseUri =
    "https://raw.githubusercontent.com/fslaborg/Deedle/master/docs/content/"

[ "design.fsx"; "frame.fsx"; "index.fsx"; "lazysource.fsx"; "rinterop.fsx";
  "series.fsx"; "stats.fsx"; "tutorial.fsx" ]
|> Seq.map (sprintf "%s%s" baseUri)
|> Seq.map (fun uri -> uri, (webclient.DownloadString(uri)))
|> Seq.iter (fun (url, txt) ->
       txt.Replace("(*[omit:(...)]*)", "").Replace("(*[/omit]*)", "")
       |> tokenize
       |> Seq.map (function
              | Markdown str -> cellTemplate "markdown" str
              | Code str -> cellTemplate "code" str
              | Other str -> cellTemplate "code" str)
       |> Seq.reduce (sprintf "%s, %s")
       |> container
       |> fun text ->
           let dst = url.Replace(baseUri, "").Replace("fsx", "ipynb")
           printfn "%s -- %s -- %s" url __SOURCE_DIRECTORY__ dst
           IO.File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, dst), text))
