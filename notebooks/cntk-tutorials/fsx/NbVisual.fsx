#r "IFSharp.Kernel"
#load "MiscellaneousHelpers.fsx"
#load "CntkHelpers.fsx"
open MathNet.Numerics.LinearAlgebra 
open XPlot.Plotly
open MiscellaneousHelpers
open CntkHelpers

/// Matrix data set converted to two dimensional scatter plot
/// <remarks> Visual helper </remarks>
let simpleScatterPlot (features:Matrix<float32>) (labels: Matrix<float32>) xTitle yTitle =
    let colors = 
        [for label in labels.[*,0] do 
            yield if label = 0.f then "Red" else "Blue"]
        
    Scatter(x = features.[*,0], y = features.[*,1], 
            mode = "markers", 
            marker = Marker(size=10, color=colors))
    |> Chart.Plot
    |> Chart.WithLayout (
            Layout( xaxis=Xaxis(title=xTitle), 
                    yaxis=Yaxis(title=yTitle)))
    |> Chart.WithHeight 400
    |> Chart.WithWidth 600  

type TrainReport = {
    BatchSize: ResizeArray<int>
    Loss: ResizeArray<float>
    Error: ResizeArray<float> }

let trainingResultPlot (plotdata : TrainReport) =
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
    

let trainingResultPlotSmoothed (plotdata : TrainReport) =    
    let avgLoss = movingAverage (plotdata.Loss) 10
    let avgError = movingAverage (plotdata.Error) 10
    let maxAvgLoss = avgLoss |> Seq.max


    [   Scatter(name="Average Loss (scaled)", line=Line(dash="dash"),
                x = plotdata.BatchSize, y = (avgLoss |> normalizeByMax maxAvgLoss))
        Scatter(name="Average Error", line=Line(dash="dash"),
                x = plotdata.BatchSize, y = avgError)]
    |> Chart.Plot
    |> Chart.WithLayout
           (Layout
                (title = "Minibatch run", xaxis = Xaxis(title = "Minibatch number"),
                 yaxis = Yaxis(title = "Cost")))
    |> Chart.WithHeight 400

let modelOutputHeatmap (range: float[]) (model: CNTK.Function) xTitle yTitle =
    let predictedLabelGrid (range : float[]) =
        seq [for x in range do for y in range do yield seq [x;y] ]
        |> evaluateWithSoftmax model
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

    Heatmap(z = (predictedLabelGrid range), colorscale = colorScale)
    |> Chart.Plot
    |> Chart.WithLayout (
            Layout( xaxis=Xaxis(title=xTitle), 
                    yaxis=Yaxis(title=yTitle)))
    |> Chart.WithWidth 700
    |> Chart.WithHeight 500