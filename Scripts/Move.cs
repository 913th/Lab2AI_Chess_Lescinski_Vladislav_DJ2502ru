public struct Move
{
    public int From, To;
    public Move(int from, int to) { From = from; To = to; }

    public override string ToString() => Name(From) + "-" + Name(To);
    static string Name(int i) => (char)('a' + i % 5) + (i / 5 + 1).ToString();
}