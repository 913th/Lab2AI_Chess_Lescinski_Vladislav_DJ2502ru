using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteGameController : MonoBehaviour
{
    [Header("Сцена")]
    public Transform chessBoard;            // если пусто, ищется объект "ChessBoard"
    public string humanPrefix = "Red";    // имена фигур человека начинаются с этого (они внизу)
    public string aiPrefix = "Black";         // имена фигур ИИ (они вверху)
    [Range(1, 6)] public int searchDepth = 4;

    [Header("Подсветка клеток")]
    public Color selectedColor = new Color(0.95f, 0.85f, 0.2f);
    public Color targetColor = new Color(0.2f, 0.7f, 0.3f);
    public Color lastMoveColor = new Color(0.25f, 0.45f, 0.8f);

    const int N = GameState.N;

    class PieceInfo
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 pos, scale;
        public Sprite sprite;
        public int sign;       // +1 человек, -1 ИИ
        public int type;       // текущий тип (меняется при превращении)
        public int origType;
    }

    SpriteRenderer[] cellSr = new SpriteRenderer[N * N];
    Color[] baseColor = new Color[N * N];
    float minX, minY, stepX, stepY;

    List<PieceInfo> pieces = new List<PieceInfo>();
    PieceInfo[] at = new PieceInfo[N * N];
    Sprite[] rookSprite = new Sprite[2];     // [0] человек, [1] ИИ
    Vector3[] rookScale = new Vector3[2];

    GameState state;
    MinimaxAI ai;
    int selected = -1;
    List<Move> legalMoves = new List<Move>();
    bool humanTurn, gameOver, ready;
    Move? lastMove;
    string statusText = "", infoText = "";

    // ---------- инициализация ----------
    void Start()
    {
        if (chessBoard == null)
        {
            var go = GameObject.Find("ChessBoard");
            if (go != null) chessBoard = go.transform;
        }
        if (!ReadBoardGeometry()) return;
        if (!ReadPieces()) return;
        ready = true;
        NewGame();
    }

    bool ReadBoardGeometry()
    {
        if (chessBoard == null) { Debug.LogError("Не найден объект ChessBoard."); return false; }

        var list = new List<SpriteRenderer>();
        foreach (var sr in chessBoard.GetComponentsInChildren<SpriteRenderer>())
            if (sr.transform != chessBoard) list.Add(sr);

        if (list.Count != N * N)
        {
            Debug.LogError($"Внутри ChessBoard ожидалось 25 клеток со Sprite Renderer, найдено {list.Count}.");
            return false;
        }

        minX = minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var sr in list)
        {
            Vector3 p = sr.transform.position;
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        stepX = (maxX - minX) / (N - 1);
        stepY = (maxY - minY) / (N - 1);
        if (stepX < 0.0001f || stepY < 0.0001f)
        {
            Debug.LogError("Клетки расставлены неправильно: не получается определить шаг сетки.");
            return false;
        }

        foreach (var sr in list)
        {
            int idx = CellAt(sr.transform.position);
            if (idx < 0 || cellSr[idx] != null)
            {
                Debug.LogError("Клетки не образуют ровную сетку 5×5: " + sr.name);
                return false;
            }
            cellSr[idx] = sr;
            baseColor[idx] = sr.color;
        }
        return true;
    }

    bool ReadPieces()
    {
        foreach (var sr in FindObjectsOfType<SpriteRenderer>())
        {
            string n = sr.gameObject.name;
            int sign = n.StartsWith(humanPrefix) ? 1 : n.StartsWith(aiPrefix) ? -1 : 0;
            if (sign == 0) continue;
            int type = n.Contains("King") ? GameState.King
                     : n.Contains("Rook") ? GameState.Rook
                     : n.Contains("Pawn") ? GameState.Pawn : 0;
            if (type == 0) continue;

            var info = new PieceInfo
            {
                go = sr.gameObject,
                sr = sr,
                pos = sr.transform.position,
                scale = sr.transform.localScale,
                sprite = sr.sprite,
                sign = sign,
                type = type,
                origType = type
            };
            pieces.Add(info);

            int side = sign > 0 ? 0 : 1;
            if (type == GameState.Rook && rookSprite[side] == null)
            {
                rookSprite[side] = sr.sprite;
                rookScale[side] = sr.transform.localScale;
            }
        }

        if (pieces.Count != 16)
            Debug.LogWarning($"Найдено фигур: {pieces.Count} (ожидалось 16). Проверьте имена: {humanPrefix}.../{aiPrefix}... + King/Rook/Pawn.");
        if (pieces.Count == 0) return false;
        return true;
    }

    // ---------- геометрия ----------
    int CellAt(Vector3 w)
    {
        int col = Mathf.RoundToInt((w.x - minX) / stepX);
        int row = Mathf.RoundToInt((w.y - minY) / stepY);
        if (col < 0 || col >= N || row < 0 || row >= N) return -1;
        if (Mathf.Abs(w.x - (minX + col * stepX)) > stepX * 0.5f) return -1;
        if (Mathf.Abs(w.y - (minY + row * stepY)) > stepY * 0.5f) return -1;
        return row * N + col;
    }

    Vector3 CellWorld(int idx, float z)
    {
        return new Vector3(minX + (idx % N) * stepX, minY + (idx / N) * stepY, z);
    }

    // ---------- новая игра ----------
    void SetupPosition()
    {
        at = new PieceInfo[N * N];
        var s = new GameState();
        foreach (var p in pieces)
        {
            p.go.SetActive(true);
            p.go.transform.position = p.pos;
            p.go.transform.localScale = p.scale;
            p.sr.sprite = p.sprite;
            p.type = p.origType;

            int idx = CellAt(p.pos);
            if (idx < 0) { Debug.LogError("Фигура вне доски: " + p.go.name); continue; }
            at[idx] = p;
            s.Board[idx] = p.sign * p.type;
        }
        s.WhiteToMove = true;
        state = s;

        int kingIdx = System.Array.IndexOf(state.Board, GameState.King);
        if (kingIdx < 0 || kingIdx / N != 0)
            Debug.LogError("Король человека должен стоять на нижней линии. Поменяйте humanPrefix/aiPrefix.");
    }

    void NewGame()
    {
        if (!ready) return;
        StopAllCoroutines();
        SetupPosition();
        ai = new MinimaxAI(searchDepth, false); // ИИ играет сверху (отрицательные фигуры)
        selected = -1;
        lastMove = null;
        gameOver = false;
        infoText = "";
        AfterMove();
    }

    // ---------- ход ----------
    void ExecuteMove(Move m)
    {
        var mover = at[m.From];
        var victim = at[m.To];
        if (victim != null) victim.go.SetActive(false);   // взятие

        at[m.To] = mover;
        at[m.From] = null;
        mover.go.transform.position = CellWorld(m.To, mover.go.transform.position.z);

        state = state.Apply(m);

        // превращение пешки в ладью
        if (mover.type == GameState.Pawn && Mathf.Abs(state.Board[m.To]) == GameState.Rook)
        {
            int side = mover.sign > 0 ? 0 : 1;
            mover.type = GameState.Rook;
            if (rookSprite[side] != null)
            {
                mover.sr.sprite = rookSprite[side];
                mover.go.transform.localScale = rookScale[side];
            }
        }
        lastMove = m;
    }

    void AfterMove()
    {
        RefreshHighlights();
        var st = state.GetStatus();
        if (st != GameStatus.Playing) { EndGame(st); return; }

        if (state.WhiteToMove) // ход человека
        {
            humanTurn = true;
            legalMoves = state.GetLegalMoves();
            statusText = "Ваш ход" + (state.IsInCheck(true) ? " — ШАХ!" : "");
        }
        else // ход ИИ
        {
            humanTurn = false;
            StartCoroutine(AITurn());
        }
    }

    IEnumerator AITurn()
    {
        statusText = "ИИ думает...";
        yield return null;
        yield return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Move m = ai.GetBestMove(state);
        sw.Stop();

        infoText = $"Ход ИИ: {m}\nОценка: {ai.LastScore}\nУзлов: {ai.NodesVisited}\n" +
                   $"Глубина: {ai.Depth}\nВремя: {sw.ElapsedMilliseconds} мс";
        selected = -1;
        ExecuteMove(m);
        AfterMove();
    }

    void EndGame(GameStatus st)
    {
        gameOver = true; humanTurn = false; selected = -1;
        legalMoves.Clear();
        if (st == GameStatus.WhiteWins) statusText = "Мат! Вы победили!";
        else if (st == GameStatus.BlackWins) statusText = "Мат! Победил ИИ.";
        else statusText = "Ничья (пат или 40 ходов без прогресса)";
        RefreshHighlights();
    }

    // ---------- ввод мышью ----------
    void Update()
    {
        if (!ready || !humanTurn || gameOver) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int idx = CellAt(w);
        if (idx >= 0) OnCellClick(idx);
    }

    void OnCellClick(int idx)
    {
        if (state.Board[idx] > 0)   // своя фигура
        {
            selected = idx;
            RefreshHighlights();
            return;
        }
        if (selected >= 0)
        {
            foreach (var m in legalMoves)
            {
                if (m.From == selected && m.To == idx)
                {
                    humanTurn = false;
                    selected = -1;
                    ExecuteMove(m);
                    AfterMove();
                    return;
                }
            }
        }
        selected = -1;
        RefreshHighlights();
    }

    // ---------- подсветка ----------
    bool IsTarget(int i)
    {
        foreach (var m in legalMoves)
            if (m.From == selected && m.To == i) return true;
        return false;
    }

    void RefreshHighlights()
    {
        for (int i = 0; i < N * N; i++)
        {
            Color c = baseColor[i];
            if (lastMove.HasValue && (i == lastMove.Value.From || i == lastMove.Value.To)) c = lastMoveColor;
            if (i == selected) c = selectedColor;
            else if (selected >= 0 && IsTarget(i)) c = targetColor;
            cellSr[i].color = c;
        }
    }

    // ---------- простой интерфейс (IMGUI) ----------
    void OnGUI()
    {
        var big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
        big.normal.textColor = Color.white;
        var small = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        small.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 20, 380, 90), statusText, big);
        GUI.Label(new Rect(20, 120, 380, 180), infoText, small);
        if (GUI.Button(new Rect(20, 320, 220, 50), "Новая игра")) NewGame();
    }
}