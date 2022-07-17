
var input = new Seq.Input.Beats.BeatsInput();

input.TransformScriptPath = "transform.js";


input.Start(Console.Out);

Console.ReadLine();

input.Stop();