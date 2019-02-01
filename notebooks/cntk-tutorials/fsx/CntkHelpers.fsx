
(*
 * Author:    Lazarus-Ares Terzopoulos
 * Created:   December 2018 - ongoing
 * 
 * (c) Licence information at https://github.com/SpaceAntelope/IfCntk
 *)


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
/// <remarks> CNTK Helper function </remarks>
let paramData<'T> (p: CNTK.Parameter) =
    let arrayView = p.Value()
    let value = new Value(arrayView)
    value.GetDenseData<'T>(p)


/// A helper function to convert a sequence
/// of numbers for use as neural network input
/// <remarks> CNTK Helper function </remarks>
let batchFromSeq (dim:int) (source : float seq) =
    CNTK.Value.CreateBatch(shape [dim], source |> Seq.map (float32), device)


/// A helper function to evaluate a dataset in
/// a softmax model and extract results in one go
/// <remarks> CNTK Helper function </remarks>
let evaluateWithSoftmax (model : Function) (source : float seq seq) =
    let inputDim = source |> Seq.head |> Seq.length
    let inputData = source |> Seq.collect id |> batchFromSeq inputDim
    let out = CNTKLib.Softmax(new Variable(model))
    let inputDataMap = [out.Arguments.[0], inputData] |> dict
    let outputDataMap = [(out.Output, null)] |> dataMap
    out.Evaluate(inputDataMap, outputDataMap, device)
    outputDataMap
        .[out.Output]
        .GetDenseData<float32>(out.Output)
    |> Seq.map Seq.head


/// Convert Function to Variable
/// <remarks> CNTK helper </remarks>
let inline Var (x : CNTK.Function) = new Variable(x)


/// Convert Variable to Function
/// <remarks> CNTK helper </remarks>
let inline Fun (x : CNTK.Variable) = x.ToFunction()


/// Create a new linear layer in the W·x+b pattern
/// <remarks> CNTK helper </helper>
let linearLayer (inputVar : Variable) outputDim =
    let inputDim = inputVar.Shape.[0]
    // Note that unlike the python example, the dimensionality of the output
    // goes first in the parameter declaration, otherwise the connection
    // cannot be propagated.
    let weightParam = new Parameter(shape [outputDim; inputDim], dataType, initialization, device, "Weights")
    let biasParam = new Parameter(shape [outputDim], dataType, 0.0, device, "Bias")
    let dotProduct = CNTKLib.Times(weightParam, inputVar, "Weighted input")
    CNTKLib.Plus(Var dotProduct, biasParam, "Layer")


/// Create a new linear layer and fully connect it to
/// an existing one through a specified differentiable function
/// <remarks> CNTK helper </helper>
let denseLayer (nonlinearity: Variable -> Function) inputVar outputDim  =
    linearLayer inputVar outputDim
    |> Var |> nonlinearity |> Var


/// Fully connected linear layer composition function
/// <remarks> CNTK helper </remarks>
let fullyConnectedClassifierNet' inputVar (hiddenLayerDims: int seq) numOutputClasses nonlinearity =
    (inputVar, hiddenLayerDims)
    ||> Seq.fold (denseLayer nonlinearity)
    |> fun model -> linearLayer model numOutputClasses


/// Evaluation of a Matrix dataset for a trained model
/// <remarks> CNTK helper </remarks>
let testMinibatch (trainer: CNTK.Trainer) (features: Matrix<float32>) (labels: Matrix<float32>) =
    let x,y = matrixToBatch features, matrixToBatch labels
    // It should be interesting to see if this convention
    // will hold for any other topography
    let input = trainer.Model().Arguments |> Seq.head
    let label = trainer.LossFunction().Arguments |> Seq.last
    let testBatch =
        [ (input, x);(label, y) ]
        |> dict
        |> AsUnorderedMapVariableValue
    trainer.TestMinibatch(testBatch , device)

