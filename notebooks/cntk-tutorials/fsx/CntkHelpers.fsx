
(* Requires MathNet to be referenced and device : CNTK.DeviceDescriptor to be set *)
open System.Collections.Generic
open MathNet.Numerics.LinearAlgebra

/// In C# a function parameter of type NDShape apparently
/// can accept a simple int array and cast away implicitly.
///
/// Not so in F#.
/// <remarks>CNTK Helper function</remarks>
let inline shape (dims:int seq) : NDShape = NDShape.CreateNDShape dims


/// A sequence of Parameter objects needs to be converted
/// to type ParameterVector in order to be passed to CNTK functions.
/// <remarks> CNTK Helper function </remarks>
let ParVec (pars:Parameter seq) =
    let vector = new ParameterVector()
    pars |> Seq.iter (vector.Add)
    vector


/// Convert MathNet 2d matrix to batch in one go, while accounting for
/// original dimensionality and numeric type.
/// <remarks> CNTK Helper function </remarks>
let matrixToBatch(m : Matrix<float32>) =
    CNTK.Value.CreateBatch(shape [m.Rank()], m |> Matrix.transpose |> Matrix.toSeq, device)


/// Convert dictionary to Variable -> Value map for CNTK
/// Ported from https://github.com/Microsoft/CNTK/blob/master/bindings/csharp/CNTKLibraryManagedDll/Helper.cs
/// <remarks> CNTK Helper function </remarks>
let AsUnorderedMapVariableValue (source: IDictionary<Variable,Value>) =
    let inputVector = new UnorderedMapVariableValuePtr()
    for pair in source do inputVector.Add(pair.Key, pair.Value)
    inputVector


/// Create System.Collections.Generic.Dictionary<Variable,Value>
/// from corresponding tuple seq. Useful when a CNTK Data Map needs
/// to be mutable, fot instance when it's going to be holding data
/// generated from our model.
/// <remarks> CNTK Helper function </remarks>
let dataMap (source: seq<Variable*Value>) =
    let result = Dictionary<Variable,Value>()
    for key,value in source do result.Add(key,value)
    result


/// A helper function to extract data from parameter nodes.
/// You can use this to see a layer's weights.
/// <remarks> CNTK Helper function </remarks>/
let paramData<'T> (p: CNTK.Parameter) =
    let arrayView = p.Value()
    let value = new Value(arrayView)
    value.GetDenseData<'T>(p)


