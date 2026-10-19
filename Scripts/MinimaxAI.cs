using System;
using System.Collections.Generic;

public class MinimaxAI
{
    public int Depth;
    public bool AiIsWhite;
    public int LastScore;
    public int NodesVisited;

    public MinimaxAI(int depth, bool aiIsWhite) { Depth = depth; AiIsWhite = aiIsWhite; }

    public Move GetBestMove(GameState state)
    {
        NodesVisited = 0;
        var moves = Order(state, state.GetLegalMoves());
        Move best = moves[0];
        int bestVal = int.MinValue;
        int alpha = int.MinValue, beta = int.MaxValue;

        foreach (var m in moves)
        {
            // после хода ИИ ходит противник -> минимизирующий уровень
            int val = Minimax(state.Apply(m), Depth - 1, false, alpha, beta);
            if (val > bestVal) { bestVal = val; best = m; }
            alpha = Math.Max(alpha, bestVal);
        }
        LastScore = bestVal;
        return best;
    }

    int Minimax(GameState s, int depth, bool isMaximizing, int alpha, int beta)
    {
        NodesVisited++;
        var moves = s.GetLegalMoves();

        // терминальные состояния
        if (moves.Count == 0)
        {
            if (s.IsInCheck(s.WhiteToMove))   // мат
                return isMaximizing ? -Evaluator.MateScore - depth : Evaluator.MateScore + depth;
            return 0;                         // пат
        }
        if (s.NoProgress >= GameState.DrawLimit) return 0;

        // ограничение глубины -> эвристика
        if (depth == 0) return Evaluator.Evaluate(s, AiIsWhite);

        moves = Order(s, moves);

        if (isMaximizing)
        {
            int best = int.MinValue;
            foreach (var m in moves)
            {
                best = Math.Max(best, Minimax(s.Apply(m), depth - 1, false, alpha, beta));
                alpha = Math.Max(alpha, best);
                if (beta <= alpha) break;
            }
            return best;
        }
        else
        {
            int best = int.MaxValue;
            foreach (var m in moves)
            {
                best = Math.Min(best, Minimax(s.Apply(m), depth - 1, true, alpha, beta));
                beta = Math.Min(beta, best);
                if (beta <= alpha) break;
            }
            return best;
        }
    }

    // Сначала взятия: так отсечений больше
    List<Move> Order(GameState s, List<Move> moves)
    {
        moves.Sort((a, b) => Math.Abs(s.Board[b.To]).CompareTo(Math.Abs(s.Board[a.To])));
        return moves;
    }
}