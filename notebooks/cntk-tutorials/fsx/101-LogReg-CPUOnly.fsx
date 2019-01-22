
(*
 * Author:    Lazarus-Ares Terzopoulos
 * Created:   December 2018 - ongoing
 * 
 * (c) Licence information at https://github.com/SpaceAntelope/IfCntk
 *)

#r "netstandard"
#r @"..\bin\Cntk.Core.Managed-2.6.dll"
#load @"..\.paket\load\main.group.fsx"
#load "MiscellaneousHelpers.fsx"
open MiscellaneousHelpers


open CNTK
let device = DeviceDescriptor.CPUDevice
#load "CntkHelpers.fsx"
open CntkHelpers
open System
open System.IO
Environment.GetEnvironmentVariable("PATH")
|> fun path -> sprintf "%s%c%s" path (Path.PathSeparator) (Path.GetFullPath("bin"))
|> fun path -> Environment.SetEnvironmentVariable("PATH", path)
(***)

DeviceDescriptor.UseDefaultDevice().Type
|> printfn "Congratulations, you are using CNTK for: %A"


// Figure 1
// ImageUrl "https://www.cntk.ai/jup/cancer_data_plot.jpg" 400


// Figure 2
// ImageUrl "https://www.cntk.ai/jup/cancer_classify_plot.jpg" 400


// Figure 3
// ImageUrl "https://www.cntk.ai/jup/logistic_neuron.jpg" 300


let featureCount = 2
let labelCount = 2
let sampleCount = 32
let device = DeviceDescriptor.CPUDevice

(*
 * No XPlot for netstandard it seems, 
 * let's use a manual reference to a .net version
 * for now
*)

// Setup display support
// #load "XPlot.Plotly.fsx"
#r @"C:\Users\%username%\.nuget\packages\xplot.plotly\1.5.0\lib\net45\XPlot.Plotly.dll"
MiscellaneousHelpers.seed <- 42

let x,y = generateRandomDataSample sampleCount featureCount labelCount

open XPlot.Plotly
open MathNet.Numerics.LinearAlgebra // necessary to slice matrixes with the bracket operator

let colors =
    [for label in y.Column(0) do
        yield if label = 0.f then "Red" else "Blue"]
Scatter(x = x.[*,0], y = x.[*,1],
        mode = "markers",
        marker = Marker(size=10, color=colors))
|> Chart.Plot
|> Chart.WithLayout (
        Layout( xaxis=Xaxis(title="Tumor size (in cm)"),
                yaxis=Yaxis(title="Age (scaled)")))
|> Chart.WithHeight 400
|> Chart.WithWidth 600
|> Chart.Show


// Figure 4
// ImageUrl "https://www.cntk.ai/jup/logistic_neuron2.jpg" 300


// "z=\sum_{i=1}^n w_i \\times x_i+b= \\textbf{w  x}+b" |> Util.Math


let dataType = CNTK.DataType.Float


let featureVariable = Variable.InputVariable(shape [|featureCount|], dataType, "Features")
let initialization = CNTKLib.GlorotUniformInitializer(1.0)
let index = System.Collections.Generic.Dictionary<string, CNTK.Parameter>()


let linearLayer (inputVar : Variable) outputDim =
    let inputDim = inputVar.Shape.[0]
    let weightParam = new Parameter(shape [inputDim; outputDim], dataType, initialization, device, "Weights")
    let biasParam = new Parameter(shape [outputDim], dataType, 0.0, device, "Bias")
    index.Add("Weights", weightParam)
    index.Add("Bias", biasParam)
    // training works for w * i and not for i * w as in the python example
    let dotProduct =  CNTKLib.Times(weightParam, inputVar, "Weighted input")
    let layer = CNTKLib.Plus(new Variable(dotProduct), biasParam, "Layer")
    layer


let z = linearLayer featureVariable labelCount


// "\\textbf{p}=softmax(z)" |> Util.Math


// "H(p)=-\sum_{j=1}^{|y|}y_j log(p_j)" |> Util.Math


let labelVariable = Variable.InputVariable(shape [labelCount], dataType, "output")
let loss = CNTKLib.CrossEntropyWithSoftmax(new Variable(z), labelVariable)


let evalError = CNTKLib.ClassificationError(new Variable(z), labelVariable)


// Instantiate the trainer object to drive the model training
let learningRate = 0.01
let lrSchedule = new CNTK.TrainingParameterScheduleDouble(learningRate, uint32 CNTK.DataUnit.Minibatch)


let learner = CNTKLib.SGDLearner(z.Parameters() |> ParVec, lrSchedule)
let trainer = CNTK.Trainer.CreateTrainer(z, loss, evalError, ResizeArray<CNTK.Learner>([learner]))


// Define a utility that prints the training progress
let printTrainingProgress (trainer: CNTK.Trainer) minibatch frequency verbose =
    if minibatch % frequency = 0
    then
        let mbla = trainer.PreviousMinibatchLossAverage()
        let mbea = trainer.PreviousMinibatchEvaluationAverage()
        if verbose then
            printfn "Minibatch: %d, Loss: %.4f, Error: %.2f" minibatch mbla mbea
        Some (minibatch, mbla, mbea)
    else None


let minibatchSize = 25
let numSamplesToTrain = 20000
let numMinibatchesToTrain = int (numSamplesToTrain/minibatchSize)
let progressOutputFreq = 50


type TrainReport = {
    BatchSize: ResizeArray<int>
    Loss: ResizeArray<float>
    Error: ResizeArray<float> }


let plotdata = {
    BatchSize = ResizeArray<int>()
    Loss = ResizeArray<float>()
    Error = ResizeArray<float>()
}


for i in [0..numMinibatchesToTrain] do
    let x,y = generateRandomDataSample minibatchSize featureCount labelCount
    let features,labels = matrixToBatch x, matrixToBatch y
    // Assign the minibatch data to the input variables and train the model on the minibatch
    let trainingBatch = [(featureVariable, features);(labelVariable, labels)] |> dict
    let status = trainer.TrainMinibatch(trainingBatch, true, device)
    // log training data
    match (printTrainingProgress trainer i progressOutputFreq true) with
    | Some (i,loss,eval) ->
        plotdata.BatchSize.Add <| i
        plotdata.Loss.Add <| loss
        plotdata.Error.Add <| eval
    | None -> ()


let lossMax = plotdata.Loss |> Seq.max
let dash = Line(dash="dash")


[   Scatter(name="Loss (scaled)", line=dash,
            x = plotdata.BatchSize,
            y = (plotdata.Loss |> normalizeByMax lossMax))
    Scatter(name="Error",
            x = plotdata.BatchSize,
            y = plotdata.Error, line=dash)]
|> Chart.Plot
|> Chart.WithLayout (Layout(title="Minibatch run",
                            xaxis=Xaxis(title="Minibatch number"),
                            yaxis=Yaxis(title="Cost")))
|> Chart.WithHeight 400
|> Chart.Show


// Compute the moving average loss to smooth out the noise in SGD
let avgLoss = movingAverage (plotdata.Loss) 10
let avgError = movingAverage (plotdata.Error) 10
let maxAvgLoss = avgLoss |> Seq.max


[   Scatter(name="Average Loss (scaled)", line=dash,
            x = plotdata.BatchSize, y = (avgLoss |> normalizeByMax maxAvgLoss))
    Scatter(name="Average Error", line=dash,
            x = plotdata.BatchSize, y = avgError)]
|> Chart.Plot
|> Chart.WithLayout
       (Layout
            (title = "Minibatch run", xaxis = Xaxis(title = "Minibatch number"),
             yaxis = Yaxis(title = "Cost")))
|> Chart.WithHeight 400
|> Chart.Show


open System.Collections.Generic


let testMinibatchSize = 25
let x_test,y_test = generateRandomDataSample testMinibatchSize featureCount labelCount
let testBatch =
    [ (featureVariable, matrixToBatch x_test)
      (labelVariable, matrixToBatch y_test) ]
    |> dict
    |> AsUnorderedMapVariableValue
trainer.TestMinibatch(testBatch, device)


/// A Function.Evaluate friendly one-hot -> boolean parser function
let parseOneHotPairs (source: IList<IList<float32>>) =
    source
    |> Seq.map Seq.head
    |> Seq.map (float>>System.Math.Round>>float32)
    |> Array.ofSeq


let out = CNTKLib.Softmax(new Variable(z))
let outputDataMap = [(out.Output, null)] |> dataMap
let inputDataMap = [(featureVariable, matrixToBatch x_test)] |> dict


// Generate network output
out.Evaluate(inputDataMap, outputDataMap, device)


// Extract data from the network
let result = outputDataMap.[out.Output].GetDenseData<float32>(out.Output)


let labelsBinary = y_test.[*,0] |> Array.ofSeq
let predictedBinary = result |> parseOneHotPairs
labelsBinary |> Array.take 10 |> printfn "Label    : %A ..."
predictedBinary |> Array.take 10 |> printfn "Predicted: %A ..."


(labelsBinary, predictedBinary)
||> Array.zip
|>  Array.countBy (fun (label,predicted) -> label = predicted)
|>  printfn "Success  : %A"


(* The index we created along with the linear layer function
   finaly comes useful!
   Seq.head is needed because the result of Value.GetDense is always 2D
*)
let weightMatrix =
    index.["Weights"]
    |> paramData<float32>
    |> Seq.head
    |> Seq.chunkBySize featureCount
    |> Array.ofSeq
let biasVector =
    index.["Bias"]
    |> paramData<float32>
    |> Seq.head


let separator_x = [0.f; biasVector.[1]/weightMatrix.[0].[0]]
let separator_y = [biasVector.[0]/weightMatrix.[0].[1]; 0.f]


separator_x, separator_y


[ Scatter(x = x.[*,0], y = x.[*,1],
          mode = "markers",
          marker = Marker(size=10, color=colors))
  Scatter(x = separator_x, y = separator_y,
          mode = "lines",
          line = Line(color="Green", width=3)) ]
|> Chart.Plot
|> Chart.WithLayout (
        Layout( xaxis=Xaxis(title="Tumor size (in cm)"),
                yaxis=Yaxis(title="Age (scaled)")))
|> Chart.WithHeight 400
|> Chart.WithWidth 600
|> Chart.Show


let predictedLabelGrid (range : float[]) =
    seq [for x in range do for y in range do yield seq [x;y] ]
    |> evaluateWithSoftmax z
    |> Array.ofSeq
    |> Array.chunkBySize range.Length


let colorScale =
    (* Scale from https://fslab.org/XPlot/chart/plotly-heatmaps.html
     * Default scales available: 'Greys' | 'Greens' | 'Bluered' | 'Hot' | 'Picnic' | 'Portland' | 'Jet' | 'RdBu' | 'Blackbody' | 'Earth' | 'Electric' | 'YIOrRd' | 'YIGnBu'
     *)
    [
        [box 0.0; box "rgb(165,0,38)"]
        [0.1111111111111111; "rgb(215,48,39)"]
        [0.2222222222222222; "rgb(244,109,67)"]
        [0.3333333333333333; "rgb(253,174,97)"]
        [0.4444444444444444; "rgb(254,224,144)"]
        [0.5555555555555556; "rgb(224,243,248)"]
        [0.6666666666666666; "rgb(171,217,233)"]
        [0.7777777777777778; "rgb(116,173,209)"]
        [0.8888888888888888; "rgb(69,117,180)"]
        [1.0; "rgb(49,54,149)"]
    ]


Heatmap(z = (predictedLabelGrid [|1. .. 0.1 .. 10.|]), colorscale = colorScale)
|> Chart.Plot
|> Chart.WithLayout (
        Layout( xaxis=Xaxis(title="Tumor size (in cm)"),
                yaxis=Yaxis(title="Age (scaled)")))
|> Chart.WithWidth 700
|> Chart.WithHeight 500
|> Chart.Show


