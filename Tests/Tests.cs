namespace Tests;

public class UnitTest1
{
    private static string Eg(string filename) => Path.Join("examples", filename);

    [Fact]
    public void Test1()
    {
        string fileContent = File.ReadAllText(Eg("ex1.cnf"));

        Assert.Contains(expectedSubstring: "cnf", fileContent);
    }
}