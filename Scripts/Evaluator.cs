using System;

public static class Evaluator
{
    public const int PawnValue = 100, RookValue = 500, KingValue = 100000;
    public const int MateScore = 1000000;

    // Оценка с точки зрения ИИ: больше = лучше для ИИ
    public static int Evaluate(GameState s, bool aiIsWhite)
    {
        int score = 0; // с точки зрения белых
        for (int i = 0; i < GameState.N * GameState.N; i++)
        {
            int p = s.Board[i];
            if (p == 0) continue;
            int sign = p > 0 ? 1 : -1;
            int row = i / GameState.N, col = i % GameState.N;
            int value = 0;

            switch (Math.Abs(p))
            {
                case GameState.Pawn:
                    int advance = sign > 0 ? row - 1 : (GameState.N - 2) - row; // сколько линий пройдено
                    value = PawnValue + 10 * advance;
                    break;
                case GameState.Rook:
                    value = RookValue + (col == 2 ? 5 : 0); // небольшой бонус за центр
                    break;
                case GameState.King:
                    value = KingValue;
                    break;
            }
            score += sign * value;
        }
        return aiIsWhite ? score : -score;
    }
}