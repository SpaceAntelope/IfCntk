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


