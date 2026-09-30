using System;
using System.Drawing;
using QFG5Extractor.Resources.Gra;

class TestGra {
    static void Main() {
        string gra = "/home/vandali/Desktop/QFG5-Tool/QFG5 Data Extracted/cdn/GRA/900000.GRA";
        string bmp = "/home/vandali/Desktop/QFG5-Tool/GRA results/test.png";
        GraConverter converter = new GraConverter();
        converter.Extract(gra, bmp, true, -1, false);
        Console.WriteLine("Extracted test.png");
    }
}
