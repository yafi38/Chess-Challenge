using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
  private const int infinity = 999999;
  private readonly int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

  private Move? lastIterationBestMove = null;
  private Move? bestMove = null;
  private bool iterationHalted = false;
  private int totalEvaluations = 0;

  public Move Think(Board board, Timer timer)
  {
    Move[] moves = board.GetLegalMoves();

    const int maxDepth = 10;
    iterationHalted = false;
    bestMove = null;
    lastIterationBestMove = null;

    for (int depth = 1; depth < maxDepth; depth++)
    {
      var score = MiniMax(board, timer, depth, -infinity, infinity, isRoot: true); ;

      Console.WriteLine("Depth: {0}, Evals: {1}", depth, totalEvaluations);
      totalEvaluations = 0;

      if (!iterationHalted)
      {
        lastIterationBestMove = bestMove;
        Console.WriteLine("Depth: {0}, Score: {1}, {2}", depth, score, lastIterationBestMove.Value.ToString());
      }
      else
      {
        break;
      }

      if (score == infinity || score == -infinity) break;
    }

    //Console.WriteLine("Evals: {0}", totalEvaluations);
    //totalEvaluations = 0;
    Console.WriteLine();

    return lastIterationBestMove ?? moves[0];
  }

  public int MiniMax(Board board, Timer timer, int remainingDepth, int alpha, int beta, bool isRoot = false)
  {
    if (remainingDepth == 0)
    {
      totalEvaluations++;
      return StaticEvaluation(board);
    }
    if (timer.MillisecondsElapsedThisTurn > 1000)
    {
      iterationHalted = true;
      return 0;
    }

    Move[] moves = board.GetLegalMoves();

    if (board.IsWhiteToMove)
    {
      int maxEval = -infinity;

      foreach (var move in moves)
      {
        if (IsCheckMateAfterMove(board, move))
        {
          if (isRoot) bestMove = move;
          return infinity;
        }

        board.MakeMove(move);
        var score = MiniMax(board, timer, remainingDepth - 1, alpha, beta);
        board.UndoMove(move);

        if (!iterationHalted && score > maxEval)
        {
          maxEval = score;
          if (isRoot) bestMove = move;
        }

        alpha = Math.Max(alpha, maxEval);

        if (alpha >= beta) break;
      }
      return maxEval;
    }
    else
    {
      int minEval = infinity;

      foreach (var move in moves)
      {
        if (IsCheckMateAfterMove(board, move))
        {
          if (isRoot) bestMove = move;
          return -infinity;
        }

        board.MakeMove(move);
        var score = MiniMax(board, timer, remainingDepth - 1, alpha, beta);
        board.UndoMove(move);

        if (!iterationHalted && score < minEval)
        {
          minEval = score;
          if (isRoot) bestMove = move;
        }

        beta = Math.Min(beta, minEval);

        if (alpha >= beta) break;
      }
      return minEval;
    }
  }

  private int StaticEvaluation(Board board)
  {
    var pieceLists = board.GetAllPieceLists();
    int score = 0;

    foreach (var pieceList in pieceLists)
    {
      score += (pieceList.IsWhitePieceList ? 1 : -1) * pieceValues[(int)pieceList.TypeOfPieceInList] * pieceList.Count;
    }

    bool[] isWhites = { true, false };

    foreach (var isWhite in isWhites)
    {
      if (board.HasKingsideCastleRight(isWhite))
      {
        score += isWhite ? 5 : -5;
      }
      if (board.HasQueensideCastleRight(isWhite))
      {
        score += isWhite ? 5 : -5;
      }
    }

    return score;
  }

  private bool IsCheckMateAfterMove(Board board, Move move)
  {
    board.MakeMove(move);
    bool isMate = board.IsInCheckmate();
    board.UndoMove(move);
    return isMate;
  }
}