using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NavalChess.Models
{
	public enum TerrainType { Sea, Island, none }
	public enum UnitType
	{
		carrier, battleship, cruiser, destroyer, submarine, mine, radar, turret
	}
	public enum GameState
	{
		Menu,
		Setup,
		PlayerDeploy,
		CombatPhase,
		Finish
	}
	public readonly record struct CellPosition
	{
		public int Row { get; init; }
		public int Col { get; init; }
		public CellPosition(int row, int col)
		{
			Row = row;
			Col = col;
		}
	}
	public class CellInfo
	{
		public TerrainType Terrain { get; set; } = TerrainType.Sea;
		public bool IsOccupied => (OccupiedBy != null);
		public bool IsCheck { get; set; } = false;
		public Ship? OccupiedBy { get; set; } = null;
		public int DectNum { get; set; } = 0;
		public bool IsDetect { get; set; } = false;

	}
	public class BoardConfig
	{
		public int Rows { get; set; } = 12;
		public int Columns { get; set; } = 8;
		public static int CellSize { get; set; } = 50;
		public int InitialBudget { get; set; } = 500;

		public BoardConfig() { }

		public BoardConfig(int rows, int cols, int cellSize, int initialBudget)
		{
			Rows = rows;
			Columns = cols;
			CellSize = cellSize;
			InitialBudget = initialBudget;
		}

	}
	public class Player
	{
		public string Name { get; private set; } = "";
		public int Budget { get; set; }
		public List<Ship> Ships { get; set; } = [];

		private Player() { }

		public static Player Create(string name, int budget)
		{
			return new Player
			{
				Name = name,
				Budget = budget
			};
		}
	}
	public static class UnitTypeHelper
	{
		public static readonly HashSet<UnitType> ShipTypes = new()
	{
		UnitType.carrier,
		UnitType.battleship,
		UnitType.cruiser,
		UnitType.destroyer,
		UnitType.submarine
	};
		public static bool IsShip(UnitType id) => ShipTypes.Contains(id);
	}

	public class UnitTypeInfo
	{
		public required string Name { get; init; }
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required string UImagePath { get; init; }
		public required string UnitImagePath { get; init; }
		public required int Price { get; init; }
		public required UnitType Id { get; init; }
		public string? Description { get; init; }
		public required TerrainType Terrain { get; init; }
		public string SizeText => $"{Width}×{Height}格";
		public bool IsShip => UnitTypeHelper.IsShip(Id);
		public string? CursorPath { get; init; }
		public int CursorRange { get; init; }
	}
	public class Cursor
	{
		public UnitType Id { get; set; }
		public string? Path { get; set; }
		public int Range { get; set; }

		public static Cursor FromUnitInfo(UnitTypeInfo template)
		{
			return new Cursor
			{
				Id = template.Id,
				Range = template.CursorRange,
				Path = template.UnitImagePath != null ? template.CursorPath : "",
			};
		}
		public HashSet<CellPosition> GetRangeGrid(CellPosition pos, int angle)
		{
			var Grid = new HashSet<CellPosition>();
			int i = 1; int j = 1;
			if (angle == 0)
			{ i = 1; j = 1; }
			else { i = -1; j = -1; }

			int startRow = pos.Row;
			int startCol = pos.Col;

			for (int r = 0; r < Range; r++)
			{
				j = j * -1;
				for (int c = 0; c < Range; c++)
				{
					i = i * -1;
					int row = startRow + (((r + 1) / 2) * j);
					int col = startCol + (((c + 1) / 2) * i);
					Grid.Add(new CellPosition(row, col));
				}
			}
			return Grid;
		}
		public string? GetStylePos(CellPosition pos, int angle)
		{
			int offsetX = 0;
			int offsetY = 0;

			var size = BoardConfig.CellSize;
			if (angle == 0)
			{
				offsetX = ((Range - 1) * -1 / 2);
				offsetY = ((Range - 1) * -1 / 2);
			}
			else if (angle == 90)
			{
				offsetX = 1;
				offsetY = ((Range - 1) * -1 / 2);
			}
			else if (angle == 180)
			{
				offsetX = ((Range + 1) * 1 / 2);
				offsetY = ((Range + 1) * 1 / 2);
			}
			else if (angle == 270)
			{
				offsetY = ((Range + 1) * 1 / 2);
			}
			else
			{ return null; }
			var top = pos.Row + offsetY;
			var left = pos.Col + offsetX + 1;
			var style = "";
			style += $"position: absolute;";
			style += $"top: {top * size}px;";
			style += $"left: {left * size}px;";
			style += $"width: {Range * size}px;";
			style += $"height: {Range * size}px;";
			style += $"background-image: url('{Path}');";
			style += "background-size: 100%;";
			style += $"transform: rotate({angle}deg);";
			style += "transform-origin: top left;";
			style += "pointer-events: none;";
			return style;
		}
	}
	public class Ship
	{

		public UnitType Id { get; set; }
		public TerrainType Terrain { get; init; }
		public int Price { get; init; }
		private int width, height;
		public int Width { get => IsVertical ? height : width; }
		public int Height { get => IsVertical ? width : height; }
		public required string ImagePath { get; set; }

		public int Angle { get; set; }
		public bool IsVertical => (Angle % 180) != 0;
		public bool IsShip => UnitTypeHelper.IsShip(Id);
		public bool IsSurvive(Dictionary<CellPosition, CellInfo> cellStates)
		{
			if (OccupyGrid == null) return false;

			return OccupyGrid.Any(pos =>
				cellStates.TryGetValue(pos, out var cell) && !cell.IsCheck
			);
		}

		private CellPosition centerPoint;
		public CellPosition CenterPoint
		{
			get => centerPoint;
			set
			{
				centerPoint = value;
				OccupyGrid = CalOccupyGrid(value);
			}
		}

		public HashSet<CellPosition>? OccupyGrid { get; private set; }
		private HashSet<CellPosition> CalOccupyGrid(CellPosition center)
		{
			var occupied = new HashSet<CellPosition>();
			int w = Width; int h = Height;
			int i = 1; int j = 1;
			if (Angle == 0) { i = 1; j = 1; }
			else if (Angle == 90) { i = -1; j = 1; }
			else if (Angle == 180) { i = -1; j = -1; }
			else if (Angle == 270) { i = 1; j = -1; }

			int startRow = center.Row;
			int startCol = center.Col;

			for (int r = 0; r < h; r++)
			{
				j = j * -1;
				for (int c = 0; c < w; c++)
				{
					i = i * -1;
					int row = startRow + (((r + 1) / 2) * j);
					int col = startCol + (((c + 1) / 2) * i);
					occupied.Add(new CellPosition(row, col));
				}
			}
			return occupied;
		}

		public static Ship FromTemplate(UnitTypeInfo template, int angle)
		{
			return new Ship
			{
				Id = template.Id,
				Angle = angle,
				width = template.Width,
				height = template.Height,
				ImagePath = template.UnitImagePath != null ? template.UnitImagePath : "",
				Terrain = template.Terrain,
				Price = template.Price,
			};
		}
		public string? GetStylePos(GameState state)
		{
			var cellSize = BoardConfig.CellSize;

			int offsetX = 0;
			int offsetY = 0;
			int CntRow = CenterPoint.Row;
			int CntCol = CenterPoint.Col;

			if (Angle == 0)
			{
				offsetX = ((Width - 1) * -1 / 2);
				offsetY = ((Height - 1) * -1 / 2);
			}
			else if (Angle == 90)
			{
				offsetX = 1;
				offsetY = ((Height - 1) * -1 / 2);
			}
			else if (Angle == 180)
			{
				offsetX = ((Width + 1) * 1 / 2);
				offsetY = ((Height + 1) * 1 / 2);
			}
			else if (Angle == 270)
			{
				offsetY = ((Height + 1) * 1 / 2);
			}
			else
			{
				return null;
			}
			int labelOffset = 0;
			if (state == GameState.CombatPhase|| state == GameState.Finish) { labelOffset = 1; }
			CntRow += offsetY;
			CntCol += offsetX + labelOffset;

			var style = "";
			style += $"position: absolute;";
			style += $"top: {CntRow * cellSize}px;";
			style += $"left: {CntCol * cellSize}px;";
			style += $"width: {width * cellSize}px;";
			style += $"height: {height * cellSize}px;";
			style += $"background-image: url('{ImagePath}');";
			style += "background-size: 100%;";
			style += $"transform: rotate({Angle}deg);";
			style += "transform-origin: top left;";
			style += "pointer-events: none;";
			return style;
		}

		public void Rotate()
		{
			Angle = (Angle + 90) % 360;
		}

	}
	public static class BoardBuilder
	{
		public static Dictionary<CellPosition, CellInfo> BuildMirroredBoard(BoardConfig config, Dictionary<CellPosition, CellInfo> leftHalf)
		{
			int rows = config.Rows;
			int halfCols = config.Columns;

			var fullBoard = new Dictionary<CellPosition, CellInfo>();

			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < halfCols; col++)
				{
					var pos = new CellPosition(row, col);
					if (!leftHalf.ContainsKey(pos)) continue;

					var baseCell = leftHalf[pos];

					// 左側
					fullBoard[pos] = new CellInfo
					{
						Terrain = baseCell.Terrain,
						IsCheck = false,
						IsDetect = false,
						DectNum = 0,
					};

					// 右側鏡射
					var mirrorCol = halfCols * 2 - 1 - col;
					var mirroredPos = new CellPosition(row, mirrorCol);

					fullBoard[mirroredPos] = new CellInfo
					{
						Terrain = baseCell.Terrain,
						IsCheck = false,
						IsDetect = false,
						DectNum = 0,
					};
				}
			}
			//標籤
			for (int row = 0; row < rows; row++)
			{
				var leftLabelPos = new CellPosition(row, -1);
				var rightLabelPos = new CellPosition(row, halfCols * 2);
				fullBoard[leftLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none};
				fullBoard[rightLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none };
			}
			for (int col = 0; col < halfCols * 2; col++)
			{
				var bottomLabelPos = new CellPosition(rows, col);
				fullBoard[bottomLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none };
			}

			return fullBoard;
		}
		public static Dictionary<CellPosition, CellInfo> BuildEmptyBoard(
			int rows,
			int cols,
			TerrainType defaultTerrain = TerrainType.Sea,
			Dictionary<CellPosition, CellInfo>? existing = null)
		{
			var board = new Dictionary<CellPosition, CellInfo>();

			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
					var pos = new CellPosition(r, c);

					if (existing != null && existing.TryGetValue(pos, out var existingCell))
					{
						// 保留原本設定
						board[pos] = new CellInfo
						{
							Terrain = existingCell.Terrain,
							IsCheck = false,
						};
					}
					else
					{
						// 新建預設地形
						board[pos] = new CellInfo
						{
							Terrain = defaultTerrain,
							IsCheck = false,
						};
					}
				}
			}
			return board;
		}
	}
}
