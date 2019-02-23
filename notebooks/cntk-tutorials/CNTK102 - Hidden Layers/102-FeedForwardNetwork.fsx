
(*
 * Author:    Lazarus-Ares Terzopoulos
 * Created:   December 2018 - ongoing
 * 
 * (c) Licence information at https://github.com/SpaceAntelope/IfCntk
 *)


//#r @"C:\Users\Ares\.nuget\packages\xplot.plotly\1.5.0\lib\net45\XPlot.Plotly.dll"

#r "netstandard"
#r @"..\bin\Cntk.Core.Managed-2.6.dll"
#load @"..\CNTK102 - Hidden Layers\.paket\load\main.group.fsx"

open CNTK
let device = DeviceDescriptor.CPUDevice
let dataType = DataType.Float
let initialization = CNTKLib.GlorotUniformInitializer(1.0)
// #load "CntkHelpers.fsx"
// open CntkHelpers
open System
open System.IO
Environment.GetEnvironmentVariable("PATH")
|> fun path -> sprintf "%s%c%s" path (Path.PathSeparator) (Path.GetFullPath("../bin"))
|> fun path -> Environment.SetEnvironmentVariable("PATH", path)
(***)

open CNTK
DeviceDescriptor.UseDefaultDevice().Type
|> printfn "Congratulations, you are using CNTK for: %A"

#load "../fsx/MiscellaneousHelpers.fsx"
#load "../fsx/NbVisual.fsx"
//open MiscellaneousHelpers

// Setup display support
//#load "AsyncDisplay.fsx"
// #load "XPlot.Plotly.fsx"


// # Figure 1
// ImageUrl "https://www.cntk.ai/jup/cancer_data_plot.jpg" 400


// # Figure 2
// ImageUrl "https://upload.wikimedia.org/wikipedia/en/5/54/Feed_forward_neural_net.gif" 200


// # Define the data dimensions
let input_dim = 2
let num_output_classes = 2


// # Create the input variables denoting the features and the label data. Note: the input
// # does not need additional info on number of observations (Samples) since CNTK first create only
// # the network topology first
let mysamplesize = 64
let features, labels = generateRandomDataSample mysamplesize input_dim num_output_classes


simpleScatterPlot "Scaled age (in yrs)" "Tumor size (in cm)" features labels


// ImageUrl "http://cntk.ai/jup/feedforward_network.jpg" 200


let num_hidden_layers = 2
let hidden_layers_dim = 50


// # The input variable (representing 1 observation, in our example of age and size) x, which
// # in this case has a dimension of 2.
// #
// # The label variable has a dimensionality equal to the number of output classes in our case 2.


let input = Variable.InputVariable(shape [|input_dim|], dataType, "Features")
let label = Variable.InputVariable(shape [|num_output_classes|], dataType, "Labels")
let initialization = CNTKLib.GlorotUniformInitializer(1.0)


// "z_1 = W·x+b" |> Util.Math


// # Define a multilayer feedforward classification model
let fullyConnectedClassifierNet inputVar numOutputClasses hiddenLayerDim numHiddenLayers nonlinearity =
    let mutable h = denseLayer nonlinearity inputVar hiddenLayerDim
    for i in 1..numHiddenLayers do
        h <- denseLayer nonlinearity h hiddenLayerDim
    // Note that we don't feed the output layer through
    // the selected nonlinearity/activation function
    linearLayer h numOutputClasses


// # Create the fully connected classifier
let z = fullyConnectedClassifierNet input num_output_classes hidden_layers_dim num_hidden_layers (CNTKLib.Sigmoid)


let z' =
    fullyConnectedClassifierNet'
        input [hidden_layers_dim;hidden_layers_dim]
        num_output_classes (CNTKLib.Sigmoid)


// "p = softmax(z_{final~layer})" |> Util.Math


// "H(p)=-\sum_{j=1}^C y_j log(p_j)" |> Util.Math


let loss = CNTKLib.CrossEntropyWithSoftmax(Var z, label)


let eval_error = CNTKLib.ClassificationError(Var z, label)


// # Instantiate the trainer object ot drive the model training
let learning_rate = 0.05
let lr_schedule = new CNTK.TrainingParameterScheduleDouble(learning_rate, uint32 CNTK.DataUnit.Minibatch)
let learner = CNTKLib.SGDLearner(z.Parameters() |> ParVec, lr_schedule)
let trainer = CNTK.Trainer.CreateTrainer(z, loss, eval_error, ResizeArray<CNTK.Learner>([learner]))


// # Initialize the parameters for the trainer
let minibatch_size = 25
let num_samples = 20000
let num_minibatches_to_train = num_samples / minibatch_size


// # Run the trainer and perform model training
let training_progress_output_freq = 20


let plotdata = {
    BatchSize = ResizeArray<int>()
    Loss = ResizeArray<float>()
    Error = ResizeArray<float>()
}


for i in [0..num_minibatches_to_train] do
    let features,labels =
        generateRandomDataSample minibatch_size input_dim num_output_classes
        |> fun (x,y) -> matrixToBatch x, matrixToBatch y
    // # Specify the input variables mapping in the model to actual minibatch data for training
    let trainingBatch = [(input, features);(label, labels)] |> dict
    let status = trainer.TrainMinibatch(trainingBatch, true, device)
    // log training data
    match (printTrainingProgress trainer i training_progress_output_freq true) with
    | Some (i,loss,eval) ->
        plotdata.BatchSize.Add <| i
        plotdata.Loss.Add <| loss
        plotdata.Error.Add <| eval
    | None -> ()


// # Compute the moving average loss to smooth out the noise in SGD
trainingResultPlotSmoothed plotdata |> Display


open MathNet.Numerics.LinearAlgebra


// # Generate new data
let test_minibatch_size = 25
let x_test,y_test = generateRandomDataSample test_minibatch_size input_dim num_output_classes


testMinibatch trainer x_test y_test


// # Figure 4
ImageUrl "http://cntk.ai/jup/feedforward_network.jpg" 200


let out = CNTKLib.Softmax(Var z)
let inputMap = [input, matrixToBatch x_test] |> dict
let outputMap = [(out.Output, null)] |> dataMap


let predicted_label_probs = out.Evaluate(inputMap, outputMap, device)


let result = outputMap.[out.Output].GetDenseData<float32>(out.Output)


y_test |> Matrix.toRowArrays |> Array.map argMax |> printfn "Label    : %A"
result |> Seq.map argMax |> Array.ofSeq |> printfn "Predicted: %A"


modelSoftmaxOutputHeatmap "Scaled age (in yrs)" "Tumor size (in cm)" [|1. .. 0.1 .. 10.|] z


open MathNet.Numerics.LinearAlgebra


// We achieve non linear separation by stealthily adding another output class,
// that we then assign to the first class, thus encircling the rest of the data.
let generateRandomNonlinearlySeparableDataSample sampleCount featureCount labelCount =
    let x,y = generateRandomDataSample sampleCount featureCount (labelCount+1)
    let y' =
        y
        |> Matrix.toRowArrays
        |> Array.map(
            fun line ->
                if line.[labelCount] = 1.f
                then line.[0] <- 1.f
                line.[0..labelCount-1])
        |> matrix
    x,y'


let rnd = new Random()
let shuffle = Seq.sortBy (fun _ -> rnd.Next())


// This slightly awkward function truncates the overpopulated class to match
// the size of the others, and makes sure the selected subset is randomly
// distributed between the two clusters (i.e. the original doubled class
// and the spurious additional set)
let stratifiedSampling (features: Matrix<float32>) (labels: Matrix<float32>) =
    let minLength =
        labels
        |> Matrix.toRowArrays
        |> Array.countBy id
        |> Array.map snd
        |> Array.min
    Seq.zip (features.ToRowArrays()) (labels.ToRowArrays())
    |> shuffle
    |> Seq.groupBy snd
    |> Seq.map (fun (key, grp) -> grp |> Seq.take minLength)
    |> Seq.collect id
    |> shuffle
    |> Seq.map (fun (f,l) -> Seq.append f l)
    |> matrix
    |> fun mtx -> mtx.[*,..1], mtx.[*,2..]


generateRandomNonlinearlySeparableDataSample 64 input_dim num_output_classes
||> simpleScatterPlot "feature 1" "feature 2"


generateRandomNonlinearlySeparableDataSample 64 input_dim num_output_classes
||> stratifiedSampling
||> simpleScatterPlot "feature 1" "feature 2"


module NonLinear =
    let inputDim, numOutputClasses = 2,2
    let learningRate = 0.005
    let minibatchSize = 100
    let trainingCycles = 15000
    let reportSampleRate = 25
    let input = Variable.InputVariable(shape [|inputDim|], dataType, "Features")
    let label = Variable.InputVariable(shape [|numOutputClasses|], dataType, "Labels")
    let z = fullyConnectedClassifierNet' input [15;10;5] numOutputClasses (CNTKLib.Sigmoid)
    let loss = CNTKLib.CrossEntropyWithSoftmax(Var z, label)
    let error = CNTKLib.ClassificationError(Var z, label)
    let lrSchedule = new CNTK.TrainingParameterScheduleDouble(learningRate, uint32 CNTK.DataUnit.Minibatch)
    let learner = CNTKLib.SGDLearner(z.Parameters() |> ParVec, lrSchedule)
    let trainer = CNTK.Trainer.CreateTrainer(z, loss, error, ResizeArray<CNTK.Learner>([learner]))


// Data logger structure
let plotdata = {
    BatchSize = ResizeArray<int>()
    Loss = ResizeArray<float>()
    Error = ResizeArray<float>()
}


for i in 0..NonLinear.trainingCycles do
    let features,labels =
        generateRandomNonlinearlySeparableDataSample
            NonLinear.minibatchSize (NonLinear.input.Shape.[0]) NonLinear.numOutputClasses
        ||> stratifiedSampling
        |> fun (x,y) -> matrixToBatch x, matrixToBatch y
    let trainingBatch = [(NonLinear.input, features);(NonLinear.label, labels)] |> dict
    let status = NonLinear.trainer.TrainMinibatch(trainingBatch, true, device)
    match (printTrainingProgress NonLinear.trainer i NonLinear.reportSampleRate false) with
    | Some (i,loss,eval) ->
        if plotdata.BatchSize.Count > 0
        then plotdata.BatchSize
             |> Seq.last
             |> (+) NonLinear.reportSampleRate
             |> plotdata.BatchSize.Add
        else plotdata.BatchSize.Add 1
        plotdata.Loss.Add <| loss
        plotdata.Error.Add <| eval
    | None -> ()


trainingResultPlotSmoothed plotdata


    [ progressBar "info" "Progress" progress
      progressBar "warning" "Loss" (loss * 100.)
      progressBar "danger" "Error" (error * 100.) ]
    |> List.reduce (+)
    |> sprintf """<div class='container'><h2>%s</h2>%s</div>""" info


open FSharp.Control


let trainCycle iterations finalCycle currentCycle htmlReport =
    // Our training cycle
    for i in 0..iterations do
        let features,labels =
            generateRandomNonlinearlySeparableDataSample NonLinear.minibatchSize
                (NonLinear.input.Shape.[0]) NonLinear.numOutputClasses
            ||> stratifiedSampling
            |> fun (x,y) -> matrixToBatch x, matrixToBatch y


        let trainingBatch =
            [(NonLinear.input, features);(NonLinear.label, labels)] |> dict


        NonLinear.trainer.TrainMinibatch(trainingBatch, true, device)
        |> ignore


        (* Let's skip the logging code to keep things shorter *)


    // Calculate training info
    let lossAverage = NonLinear.trainer.PreviousMinibatchLossAverage()
    let evaluationAverage = NonLinear.trainer.PreviousMinibatchEvaluationAverage()
    let current = 100. * (float currentCycle + 1.)/(float finalCycle)


    async {
        // Create report text
        let progress =
            sprintf "[%s] %.1f%%" ("".PadLeft(int current,'=').PadRight(100,' ')) current
        let info =
            sprintf "Minibatch: %d of %d, Loss: %.4f, Error: %.2f"
                ((currentCycle+1)*iterations) (finalCycle * iterations) lossAverage evaluationAverage;
        let progressBar =
            if htmlReport then
                reportHtml info current lossAverage evaluationAverage
            else
                sprintf "<pre>%s\n %s</pre>" progress info
        // Send result to AsyncSeq
        return progressBar |> Util.Html
    }


let totalCycles = 128
let iterationsPerCycle = NonLinear.trainingCycles / totalCycles


AsyncSeq.initAsync (int64 totalCycles)
    (fun i -> trainCycle iterationsPerCycle totalCycles (int i) false)
|> Display


let test_minibatch_size = 128
let x_test,y_test = generateRandomNonlinearlySeparableDataSample test_minibatch_size input_dim num_output_classes


testMinibatch NonLinear.trainer x_test y_test


modelSoftmaxOutputHeatmap "feature 1" "feature 2" [|0. .. 0.1 .. 15.|] NonLinear.z



Function.Save("nonlinear2.model")
