using System;
using System.Collections.Generic;

public enum GameStatus { Playing, WhiteWins, BlackWins, Draw }

public class GameState
{
    public const int N = 5;
    public const int Pawn = 1, Rook = 2, King = 3;
    public const int DrawLimit = 40;

    public int[] Board = new int[N * N];
    public bool WhiteToMove = true;
    public int NoProgress;

    public static GameState Initial()
    {
        var s = new GameState();
        s.Board[0] = Rook; s.Board[2] = King; s.Board[4] = Rook;
        s.Board[4 * N + 0] = -Rook; s.Board[4 * N + 2] = -King; s.Board[4 * N + 4] = -Rook;
        for (int c = 0; c < N; c++)
        {
            s.Board[1 * N + c] = Pawn;
            s.Board[3 * N + c] = -Pawn;
        }
        return s;
    }

    public GameState Clone()
    {
        var s = new GameState();
        Array.Copy(Board, s.Board, Board.Length);
        s.WhiteToMove = WhiteToMove;
        s.NoProgress = NoProgress;
        return s;
    }

    // Применение хода: возвращает НОВОЕ состояние
    public GameState Apply(Move m)
    {
        var s = Clone();
        int piece = s.Board[m.From];
        bool capture = s.Board[m.To] != 0;

        s.Board[m.To] = piece;
        s.Board[m.From] = 0;

        // превращение пешки в ладью
        if (piece == Pawn && m.To / N == N - 1) s.Board[m.To] = Rook;
        if (piece == -Pawn && m.To / N == 0) s.Board[m.To] = -Rook;

        s.NoProgress = (capture || Math.Abs(piece) == Pawn) ? 0 : NoProgress + 1;
        s.WhiteToMove = !WhiteToMove;
        return s;
    }

    static bool In(int r, int c) => r >= 0 && r < N && c >= 0 && c < N;

    // Ходы без проверки на шах
    List<Move> PseudoMoves(bool white)
    {
        var moves = new List<Move>();
        int sign = white ? 1 : -1;
        int[] dr = { 1, -1, 0, 0 };
        int[] dc = { 0, 0, 1, -1 };

        for (int i = 0; i < N * N; i++)
        {
            int p = Board[i];
            if (p == 0 || (p > 0) != white) continue;
            int r = i / N, c = i % N;

            switch (Math.Abs(p))
            {
                case Pawn:
                    int nr = r + sign;
                    if (nr >= 0 && nr < N)
                    {
                        if (Board[nr * N + c] == 0) moves.Add(new Move(i, nr * N + c));
                        for (int d = -1; d <= 1; d += 2)
                        {
                            int nc = c + d;
                            if (In(nr, nc) && Board[nr * N + nc] * sign < 0)
                                moves.Add(new Move(i, nr * N + nc));
                        }
                    }
                    break;

                case Rook:
                    for (int k = 0; k < 4; k++)
                    {
                        int rr = r + dr[k], cc = c + dc[k];
                        while (In(rr, cc))
                        {
                            int t = Board[rr * N + cc];
                            if (t == 0) moves.Add(new Move(i, rr * N + cc));
                            else
                            {
                                if (t * sign < 0) moves.Add(new Move(i, rr * N + cc));
                                break;
                            }
                            rr += dr[k]; cc += dc[k];
                        }
                    }
                    break;

                case King:
                    for (int a = -1; a <= 1; a++)
                        for (int b = -1; b <= 1; b++)
                        {
                            if (a == 0 && b == 0) continue;
                            int rr = r + a, cc = c + b;
                            if (In(rr, cc) && Board[rr * N + cc] * sign <= 0)
                                moves.Add(new Move(i, rr * N + cc));
                        }
                    break;
            }
        }
        return moves;
    }

    public bool IsInCheck(bool white)
    {
        int king = white ? King : -King;
        int idx = Array.IndexOf(Board, king);
        if (idx < 0) return false;
        foreach (var m in PseudoMoves(!white))
            if (m.To == idx) return true;
        return false;
    }

    // Все допустимые ходы текущего игрока (король не остаётся под шахом)
    public List<Move> GetLegalMoves()
    {
        var result = new List<Move>();
        foreach (var m in PseudoMoves(WhiteToMove))
            if (!Apply(m).IsInCheck(WhiteToMove)) result.Add(m);
        return result;
    }

    // Проверка терминального состояния
    public GameStatus GetStatus()
    {
        if (GetLegalMoves().Count == 0)
        {
            if (IsInCheck(WhiteToMove))
                return WhiteToMove ? GameStatus.BlackWins : GameStatus.WhiteWins; // мат
            return GameStatus.Draw; // пат
        }
        if (NoProgress >= DrawLimit) return GameStatus.Draw;
        return GameStatus.Playing;
    }
}