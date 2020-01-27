﻿#region Using
using OTAPI.Tile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
#endregion
namespace FakeProvider
{
    public sealed class TileProvider<T> : INamedTileCollection
    {
        #region Data

        public static readonly ushort[] SignTileTypes = new ushort[] { TileID.Signs, TileID.AnnouncementBox, TileID.Tombstones };
        public static readonly ushort[] ChestTileTypes = new ushort[] { TileID.Containers, TileID.Containers2, TileID.Dressers };

        public TileProviderCollection ProviderCollection { get; internal set; }
        private Tile<T>[,] Data;
        public string Name { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Layer { get; private set; }
        public bool Enabled { get; private set; } = false;
        private List<FakeSign> _Signs = new List<FakeSign>();
        public ReadOnlyCollection<FakeSign> Signs => new ReadOnlyCollection<FakeSign>(_Signs);
        private List<FakeChest> _Chests = new List<FakeChest>();
        public ReadOnlyCollection<FakeChest> Chests => new ReadOnlyCollection<FakeChest>(_Chests);
        private object Locker = new object();

        #endregion
        #region Constructor

        internal TileProvider() { }

        #endregion
        #region Initialize

        public void Initialize(TileProviderCollection ProviderCollection, string Name, int X, int Y,
            int Width, int Height, int Layer = 0)
        {
            if (this.Name != null)
                throw new Exception("Attempt to reinitialize.");
            this.ProviderCollection = ProviderCollection;
            this.Name = Name;
            this.Data = new Tile<T>[Width, Height];
            this.X = X;
            this.Y = Y;
            this.Width = Width;
            this.Height = Height;
            this.Layer = Layer;

            for (int x = 0; x < this.Width; x++)
                for (int y = 0; y < this.Height; y++)
                    Data[x, y] = new Tile<T>();
        }

        public void Initialize(TileProviderCollection ProviderCollection, string Name, int X, int Y,
            int Width, int Height, ITileCollection CopyFrom, int Layer = 0)
        {
            if (this.Name != null)
                throw new Exception("Attempt to reinitialize.");
            this.ProviderCollection = ProviderCollection;
            this.Name = Name;
            this.Data = new Tile<T>[Width, Height];
            this.X = X;
            this.Y = Y;
            this.Width = Width;
            this.Height = Height;
            this.Layer = Layer;

            for (int i = X; i < X + Width; i++)
                for (int j = Y; j < Y + Height; j++)
                {
                    ITile t = CopyFrom[i, j];
                    if (t != null)
                        Data[i - X, j - Y] = new Tile<T>(t);
                }
        }

        public void Initialize(TileProviderCollection ProviderCollection, string Name, int X, int Y,
            int Width, int Height, ITile[,] CopyFrom, int Layer = 0)
        {
            if (this.Name != null)
                throw new Exception("Attempt to reinitialize.");
            this.ProviderCollection = ProviderCollection;
            this.Name = Name;
            this.Data = new Tile<T>[Width, Height];
            this.X = X;
            this.Y = Y;
            this.Width = Width;
            this.Height = Height;
            this.Layer = Layer;

            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                {
                    ITile t = CopyFrom[i, j];
                    if (t != null)
                        Data[i, j] = new Tile<T>(t);
                }
        }

        #endregion

        #region operator[,]

        ITile ITileCollection.this[int X, int Y]
        {
            get => Data[X, Y];
            set => Data[X, Y].CopyFrom(value);
        }

        public IProviderTile this[int X, int Y]
        {
            get => Data[X, Y];
            set => Data[X, Y].CopyFrom(value);
        }

        #endregion
        #region GetTileSafe

        public IProviderTile GetTileSafe(int X, int Y) => X >= 0 && Y >= 0 && X < Width && Y < Height
            ? Data[X, Y]
            : FakeProvider.VoidTile;

        #endregion

        #region XYWH

        public (int X, int Y, int Width, int Height) XYWH(int DeltaX = 0, int DeltaY = 0) =>
            (X + DeltaX, Y + DeltaY, Width, Height);

        #endregion
        #region ClampXYWH

        public (int X, int Y, int Width, int Height) ClampXYWH() =>
            (ProviderCollection.Clamp(X, Y, Width, Height));

        #endregion
        #region SetXYWH

        public void SetXYWH(int X, int Y, int Width, int Height)
        {
            this.X = X;
            this.Y = Y;
            if ((this.Width != Width) || (this.Height != Height))
            {
                Tile<T>[,] newData = new Tile<T>[Width, Height];
                for (int i = 0; i < Width; i++)
                    for (int j = 0; j < Height; j++)
                        if ((i < this.Width) && (j < this.Height))
                            newData[i, j] = Data[i, j];
                        else
                            newData[i, j] = new Tile<T>();
                this.Data = newData;
                this.Width = Width;
                this.Height = Height;
            }
        }

        #endregion
        #region Move

        public void Move(int X, int Y, bool Draw = true)
        {
            bool wasEnabled = Enabled;
            if (wasEnabled)
                Disable(Draw);
            SetXYWH(X, Y, this.Width, this.Height);
            if (wasEnabled)
                Enable(Draw);
        }

        #endregion
        #region Enable

        public void Enable(bool Draw = true)
        {
            if (!Enabled)
            {
                Enabled = true;
                ProviderCollection.UpdateProviderReferences(this);
                if (Draw)
                    this.Draw(true);
            }
        }

        #endregion
        #region Disable

        public void Disable(bool Draw = true)
        {
            if (Enabled)
            {
                Enabled = false;
                // Adding/removing manually added/removed signs, chests and entities
                Scan();
                // Remove signs, chests, entities
                HideSignsChestsEntities();
                // Showing tiles, signs, chests and entities under the provider
                ProviderCollection.UpdateRectangleReferences(X, Y, Width, Height);
                if (Draw)
                    this.Draw(true);
            }
        }

        #endregion
        #region SetTop

        public void SetTop(bool Draw = true)
        {
            ProviderCollection.SetTop(this);
            ProviderCollection.UpdateProviderReferences(this);
            if (Draw)
                this.Draw();
        }

        #endregion
        #region HideSignsChestsEntities

        public void HideSignsChestsEntities()
        {
            lock (Locker)
            {
                foreach (FakeSign sign in _Signs.ToArray())
                    HideSign(sign);
                foreach (FakeChest chest in _Chests.ToArray())
                    HideChest(chest);
            }
        }

        #endregion
        #region UpdateSignsChestsEntities

        /// <summary>
        /// Shows signs, chests and entities in Main.sign, Main.chest, ___ where
        /// the tile of this provider is on top of the ProviderCollection Tile map
        /// and hides otherwise.
        /// </summary>
        public void UpdateSignsChestsEntities()
        {
            UpdateSigns();
            UpdateChests();
        }

        #endregion
        #region Scan

        /// <summary>
        /// Adds manually added signs, chests and entities to the provider;
        /// Removes irrelevant signs, chests and entities from the provider.
        /// The method doesn't actually show things in Main.sign/Main.chest/...
        /// </summary>
        public void Scan()
        {
            ScanSigns();
            ScanChests();
        }

        #endregion
        #region TileOnTop

        public bool TileOnTop(int X, int Y) =>
            ProviderCollection.GetTileSafe(this.X + X, this.Y + Y).Provider == this;

        #endregion

        #region AddSign

        public FakeSign AddSign(int X, int Y, string Text)
        {
            FakeSign sign = new FakeSign(this, -1, X, Y, Text);
            lock (Locker)
            {
                _Signs.RemoveAll(s => s.x == sign.x && s.y == sign.y);
                _Signs.Add(sign);
            }
            UpdateSign(sign);
            return sign;
        }

        #endregion
        #region RemoveSign

        public void RemoveSign(FakeSign Sign)
        {
            lock (Locker)
            {
                HideSign(Sign);
                if (!_Signs.Remove(Sign))
                    throw new Exception("No such sign in this tile provider.");
            }
        }

        #endregion
        #region UpdateSigns

        public void UpdateSigns()
        {
            lock (Locker)
                foreach (FakeSign sign in _Signs.ToArray())
                    UpdateSign(sign);
        }

        #endregion
        #region UpdateSign

        private bool UpdateSign(FakeSign Sign)
        {
            if (IsSignTile(Sign.RelativeX, Sign.RelativeY) && TileOnTop(Sign.RelativeX, Sign.RelativeY))
                return ApplySign(Sign);
            else
                HideSign(Sign);
            return true;
        }

        #endregion
        #region ApplySign

        private bool ApplySign(FakeSign Sign)
        {
            Sign.x = ProviderCollection.OffsetX + this.X + Sign.RelativeX;
            Sign.y = ProviderCollection.OffsetY + this.Y + Sign.RelativeY;
            if (Sign.Index >= 0 && Main.sign[Sign.Index] == Sign)
                return true;

            bool applied = false;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.sign[i] != null && Main.sign[i].x == Sign.x && Main.sign[i].y == Sign.y)
                    Main.sign[i] = null;
                if (!applied && Main.sign[i] == null)
                {
                    applied = true;
                    Main.sign[i] = Sign;
                    Sign.Index = i;
                }
            }
            return applied;
        }

        #endregion
        #region HideSign

        private void HideSign(FakeSign sign)
        {
            if (sign.Index >= 0 && Main.sign[sign.Index] == sign)
                Main.sign[sign.Index] = null;
        }

        #endregion
        #region ScanSigns

        private void ScanSigns()
        {
            lock (Locker)
                foreach (FakeSign sign in _Signs.ToArray())
                    if (!IsSignTile(sign.RelativeX, sign.RelativeY))
                        RemoveSign(sign);

            (int x, int y, int width, int height) = XYWH(ProviderCollection.OffsetX, ProviderCollection.OffsetY);
            for (int i = 0; i < 1000; i++)
            {
                Sign sign = Main.sign[i];
                if (sign == null)
                    continue;

                if (sign.GetType().Name == "Sign" // <=> not FakeSign or some other inherited type
                    && Helper.Inside(sign.x, sign.y, x, y, width, height)
                    && TileOnTop(sign.x - this.X, sign.y - this.Y))
                {
                    if (IsSignTile(sign.x - this.X, sign.y - this.Y))
                        AddSign(sign.x - x, sign.y - y, sign.text);
                    else
                        Main.sign[i] = null;
                }
            }
        }

        #endregion
        #region IsSignTile

        private bool IsSignTile(int X, int Y)
        {
            ITile providerTile = GetTileSafe(X, Y);
            return providerTile.active() && SignTileTypes.Contains(providerTile.type);
        }

        #endregion

        #region AddChest

        public FakeChest AddChest(int X, int Y, Item[] Items = null)
        {
            FakeChest chest = new FakeChest(this, -1, X, Y, Items);
            lock (Locker)
            {
                _Chests.RemoveAll(c => c.x == chest.x && c.y == chest.y);
                _Chests.Add(chest);
            }
            UpdateChest(chest);
            return chest;
        }

        #endregion
        #region RemoveChest

        public void RemoveChest(FakeChest Chest)
        {
            lock (Locker)
            {
                HideChest(Chest);
                if (!_Chests.Remove(Chest))
                    throw new Exception("No such sign in this tile provider.");
            }
        }

        #endregion
        #region UpdateChests

        public void UpdateChests()
        {
            lock (Locker)
                foreach (FakeChest chest in _Chests.ToArray())
                    UpdateChest(chest);
        }

        #endregion
        #region UpdateChest

        private bool UpdateChest(FakeChest Chest)
        {
            if (IsChestTile(Chest.RelativeX, Chest.RelativeY) && TileOnTop(Chest.RelativeX, Chest.RelativeY))
                return ApplyChest(Chest);
            else
                HideChest(Chest);
            return true;
        }

        #endregion
        #region ApplyChest

        private bool ApplyChest(FakeChest Chest)
        {
            Chest.x = ProviderCollection.OffsetX + this.X + Chest.RelativeX;
            Chest.y = ProviderCollection.OffsetY + this.Y + Chest.RelativeY;
            if (Chest.Index >= 0 && Main.chest[Chest.Index] == Chest)
                return true;

            bool applied = false;
            for (int i = 0; i < 1000; i++)
            {
                if (Main.chest[i] != null && Main.chest[i].x == Chest.x && Main.chest[i].y == Chest.y)
                    Main.chest[i] = null;
                if (!applied && Main.chest[i] == null)
                {
                    applied = true;
                    Main.chest[i] = Chest;
                    Chest.Index = i;
                }
            }
            return applied;
        }

        #endregion
        #region HideChest

        private void HideChest(FakeChest Chest)
        {
            if (Chest.Index >= 0 && Main.chest[Chest.Index] == Chest)
                Main.chest[Chest.Index] = null;
        }

        #endregion
        #region ScanChests

        private void ScanChests()
        {
            lock (Locker)
                foreach (FakeChest chest in _Chests.ToArray())
                    if (!IsChestTile(chest.RelativeX, chest.RelativeY))
                        RemoveChest(chest);

            (int x, int y, int width, int height) = XYWH(ProviderCollection.OffsetX, ProviderCollection.OffsetY);
            for (int i = 0; i < 1000; i++)
            {
                Chest chest = Main.chest[i];
                if (chest == null)
                    continue;

                if (chest.GetType().Name == "Chest" // <=> not FakeChest or some other inherited type
                    && Helper.Inside(chest.x, chest.y, x, y, width, height)
                    && TileOnTop(chest.x - this.X, chest.y - this.Y))
                {
                    if (IsChestTile(chest.x - this.X, chest.y - this.Y))
                        AddChest(chest.x - x, chest.y - y, chest.item);
                    else
                        Main.chest[i] = null;
                }
            }
        }

        #endregion
        #region IsChestTile

        private bool IsChestTile(int X, int Y)
        {
            ITile providerTile = GetTileSafe(X, Y);
            return providerTile.active() && ChestTileTypes.Contains(providerTile.type);
        }

        #endregion

        #region Draw

        public void Draw(bool Section = true)
        {
            if (Section)
            {
                NetMessage.SendData((int)PacketTypes.TileSendSection, -1, -1, null, X, Y, Width, Height);
                int sx1 = Netplay.GetSectionX(X), sy1 = Netplay.GetSectionY(Y);
                int sx2 = Netplay.GetSectionX(X + Width - 1), sy2 = Netplay.GetSectionY(Y + Height - 1);
                NetMessage.SendData((int)PacketTypes.TileFrameSection, -1, -1, null, sx1, sy1, sx2, sy2);
            }
            else
                NetMessage.SendData((int)PacketTypes.TileSendSquare, -1, -1, null, Math.Max(Width, Height), X, Y);
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (Data == null)
                return;
            Disable();
            Data = null;
        }

        #endregion
    }
}