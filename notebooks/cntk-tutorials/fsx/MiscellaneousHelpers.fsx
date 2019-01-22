
(*
 * Author:    Lazarus-Ares Terzopoulos
 * Created:   December 2018 - ongoing
 * 
 * (c) Licence information at https://github.com/SpaceAntelope/IfCntk
 *)

/// <remarks> Helper function </remarks>
let inline normalizeByMax(max:'T) (source : 'T seq) =
    source |> Seq.map ((fun n -> float n/ float max)>>float32)


/// Define a utility function to compute the moving average.
/// A more efficient implementation is possible with np.cumsum() function
/// <remarks> Helper Function.
/// *Summary from comments in python notebook</remarks>
let movingAverage (array : float seq) windowLength =
    if (array |> Seq.length) >= windowLength
    then array
         |> Seq.windowed windowLength
         |> Seq.map (Seq.average)
    else seq [array |> Seq.average]


open MathNet.Numerics.Distributions;


let mutable seed = 42
let rand = System.Random(seed)
let nrand = Normal(0.,1.,rand)
let randInt max = seq { while true do yield rand.Next() % max }
let randn = Normal.Samples(rand, 0.0, 1.0)
let oneHotEncoding classCount classType =
    Array.init classCount (fun i -> if i = classType then 1.0f else 0.0f)


open MathNet.Numerics.LinearAlgebra

/// Synthetic data generator
/// <remarks>Helper function</remarks>
let generateRandomDataSample sampleCount featureCount labelCount =
    let Y = Array.init sampleCount
                (fun _ -> float32 (rand.Next() % labelCount) )
    let X = DenseMatrix.init sampleCount featureCount
                (fun row col -> float32 (nrand.Sample() + 3.) * (Y.[row]+1.f) )
    let oneHotLabel =
        Y
        |> Array.map(int>>(oneHotEncoding labelCount))
        |> DenseMatrix.ofRowArrays


    X, oneHotLabel

