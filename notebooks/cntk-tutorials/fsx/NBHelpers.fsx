
(*
 * Author:    Lazarus-Ares Terzopoulos
 * Created:   December 2018 - ongoing
 * 
 * (c) Licence information at https://github.com/SpaceAntelope/IfCntk
 *)



// Without this reference the script fails 
// to #load in notebook, claiming 
// Util is not defined
#r "IFSharp.Kernel"

/// Simple wrapper to show inline images
/// from url with customizable width
/// <remarks> Notebook Helper Function </remarks>
let ImageUrl url width =
    sprintf "<img src=\"%s\" style=\"width: %dpx; height: auto\" alt=\"Could not load image, make sure url is correct\">" url width
    |> Util.Html
    |> Display


