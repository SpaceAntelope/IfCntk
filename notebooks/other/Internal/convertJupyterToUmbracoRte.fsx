#load ".paket/load/main.group.fsx"

open Fizzler.Systems.HtmlAgilityPack
open HtmlAgilityPack
open System
open System.Text.RegularExpressions
open System.IO

let trunc ln (str : string) =
    let real_ln = str.Length
    str.Substring(0, Math.Min(ln, real_ln))

let convert (source : string) =
    let output = source.Replace(".html", "_output.html")
    let doc = HtmlDocument()
    doc.Load(source)
    let (|TextCell|CodeCell|Other|) (node : HtmlNode) =
        match node.Attributes.["class"].Value with
        | css when css.Contains("text_cell") -> TextCell node
        | css when css.Contains("code_cell") -> CodeCell node
        | _ -> Other node
    doc.DocumentNode.QuerySelectorAll(".cell")
    |> Seq.collect (function
           | TextCell node ->
               let text = node.QuerySelector(".text_cell_render")
               text.Attributes.Remove("class")
               let anchor = text.QuerySelectorAll("a.anchor-link")
               if (anchor <> null) then
                   while anchor
                         |> Seq.length > 0 do
                       (anchor |> Seq.head).Remove()
               
               let alert = node.QuerySelector(".alert")
               if alert <> null && alert.QuerySelector(".fas") = null then
                   match alert.Attributes.["class"].Value with
                   | cls when cls.Contains("-warning") -> """<span class="fas fa-exclamation-triangle fa-2x" style="margin: 10px; margin-left: 0; float: left;">""" 
                   | cls when cls.Contains("-info") -> """<span class="fas fa-info-circle fa-2x" style="margin: 10px; margin-left: 0; float: left;">""" 
                   |> HtmlNode.CreateNode |> alert.PrependChild
                   |> ignore
               
               let label = node.QuerySelector(".label")
               if label <> null then 
                let cls = label.Attributes.["class"].Value
                label.Attributes.["class"].Remove()
                label.Attributes.Add("class", cls.Replace("label", "badge"))
                label.Attributes.Add("style", "margin: 5px;")
                
               [ text ]
           | CodeCell node ->
               let code =
                   node.QuerySelector(".input_area pre").InnerText
                   |> sprintf
                          "<pre class=\"line-numbers language-fsharp\"><code>%s</code></pre>"
                   |> HtmlNode.CreateNode

               let output =
                   match node.QuerySelector(".output_area") with
                   | null -> null
                   | node ->
                       //printfn "%s, %s" (node.FirstChild.Name) (trunc 30 <| node.InnerText.Trim())
                       if (node.Descendants("script") |> Seq.length) > 0 then
                           HtmlNode.CreateNode
                               "<div><span style=\"font-family: 'Courier New', Courier, monospace\">Output:</span><div style='border: solid gold 3px; border-radius: 5px; padding: 5px;'><img src='' alt='There should probably be a chart image here'</div></div>"
                       else if node.FirstChild.Name = "#text" then
                           let result =
                               Regex.Replace
                                   (node.OuterHtml,
                                    @"((\r\n|\n|\r)$)|(^(\r\n|\n|\r))|^\s*$", "",
                                    RegexOptions.Multiline)
                           //result |> trunc 200 |> printfn "[[[[ %s ]]]]"
                           result |> HtmlNode.CreateNode
                       else
                           node.FirstChild
                           |> fun outputNode ->
                               outputNode.OuterHtml.Replace("\n\n", "\n")
                               //Regex.Replace(node.OuterHtml, "^\\s*$", "·", RegexOptions.Multiline)
                               |> sprintf
                                      "<div><span style=\"font-family: 'Courier New', Courier, monospace\">Output:</span>%s</div>"
                               |> HtmlNode.CreateNode

               [ code; output ]
           | _ -> [])
    |> Seq.filter (isNull >> not)
    |> Seq.map (fun node -> node.OuterHtml)
    |> fun lines -> File.WriteAllLines(output, lines)

let countClasses() =
    let doc' = HtmlDocument()
    doc'.Load("output.html")
    doc'.DocumentNode.QuerySelectorAll("code span")
    |> Seq.filter (fun node -> node.Attributes.Contains("class"))
    |> Seq.map (fun node -> node.Attributes.["class"].Value)
    |> Seq.filter (isNull >> not)
    |> Seq.collect (fun str -> str.Split(' '))
    //|> Seq.distinct
    |> Seq.countBy id

convert @"notebooks\cntk-tutorials\101-LogReg-CPUOnly.html"

// "<div></div>"
// |> HtmlNode.CreateNode
// |> fun node -> node.QuerySelector("img")