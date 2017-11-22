using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
    public interface ISituation
    {
        VehileCollection Vehiles { get; }
        VehileCollection EnemyVehiles { get; }
        Player Me { get; }
        World World { get; }
        Game Game { get; }
        Move Move { get; }
        ICommandCollection Commands { get; }
        bool CanNuclearStrike { get; }
        StartMatrix StartMatrix { get; }
        ILog Log { get; }
    }

    public sealed class MyStrategy : IStrategy, ISituation
    {
        public const int GroupCount = 5;
        private readonly VehicleType[] _types = { VehicleType.Arrv, VehicleType.Fighter, VehicleType.Helicopter, VehicleType.Ifv, VehicleType.Tank };
        public bool CanNuclearStrike { get; private set; }
        public ILog Log { get; }

        public VehileCollection Vehiles { get; }
        public VehileCollection EnemyVehiles { get; }

        public Player Me { get; private set; }
        public World World { get; private set; }
        public Game Game { get; private set; }
        public Move Move { get; private set; }
        public ICommandCollection Commands { get; }
        Command Command { get; set; }

        public StartMatrix StartMatrix { get; }

        public MyStrategy()
        {
            StartMatrix = new StartMatrix();
            Vehiles = new VehileCollection();
            EnemyVehiles = new VehileCollection();

            Commands = new CommandCollection(this);
            Commands.Add(new DeployCommand2());
            Commands.Add(new NuclearStriceCommand());


            Log = new FileLogger();

        }

        private int _availibleActionCount;

        void IStrategy.Move(Player me, World world, Game game, Move move)
        {
            if (world.TickIndex % game.ActionDetectionInterval == 0)
                _availibleActionCount = game.BaseActionCount;

            CanNuclearStrike = me.RemainingNuclearStrikeCooldownTicks <= 0;

            Move = move;
            Me = me;
            World = world;
            Game = game;

            Trace($"********************************** TickIndex = {world.TickIndex} AvailibleActionCount={_availibleActionCount} NextNuclearStrikeTickIndex={me.NextNuclearStrikeTickIndex}");

            Vehiles.Initialize(me, world);
            EnemyVehiles.Initialize(world.GetOpponentPlayer(), world);

            if (world.TickIndex == 0)
            {
                StartMatrix.BuilMatrix(Vehiles);

                var matrix = StartMatrix.Storage;
                Trace("################ StartMatrix ##############");
                for (int i = 0; i < 3; i++)
                    Trace("{0}\t{1}\t{2}", GetInfo(matrix[i, 0]), GetInfo(matrix[i, 1]), GetInfo(matrix[i, 2]));

                Trace("################ StartMatrix ##############");
            }
            else if (world.TickIndex == 1)
            {
            }

            foreach (var type in _types)
            {
                Trace("{0} {1}", type, Vehiles.GetVehileRect(type));
            }

            if (EnemyVehiles.Count > 0)
            {
                Trace("##### ENEMIES ####### {0}", Vehiles.GetRect());
                foreach (var type in _types)
                {
                    Trace("{0} {1}", type, Vehiles.GetVehileRect(type));
                }
            }

            if (_availibleActionCount > 0)
            {
                if (Command == null)
                    Command = Commands.GetToPropcess();
                if (Command != null)
                {
                    Command.Situation = this;
                    Command.Do();
                    Commands.Remove(Command);

                    Command = Command.Next;
                    _availibleActionCount--;
                }
            }

            Trace();
        }

        private string GetInfo(VecGroup v)
        {
            return v?.Type.ToString() ?? "NULL";
        }

        private void Trace()
        {
            Log?.Log(Move);
        }

        private void Trace(object text)
        {
            Log?.Log(text.ToString());
        }
        private void Trace(string text)
        {
            Log?.Log(text);
        }

        private void Trace(string text, params object[] args)
        {
            Log?.Log(text, args);
        }

    }

    public class DeployCommand2 : SelectUnitCommand
    {
        private readonly PointF _topDestination = new PointF(200, 100);
        private readonly PointF _bottomDestination = new PointF(200, 200);
        protected override void DoImpl()
        {
            var gr = Strategy.StartMatrix.GetFirstRight(VehicleType.Arrv);

            if (gr == null)
                return;


            Type = gr.Type;
            var pairType = GetPairType(Type);
            var pairGr = Strategy.StartMatrix.Storage.OfType<VecGroup>().First(e => e.Type == pairType);

            VecGroup bottomGroup = null;
            VecGroup topGroup = null;
            if (gr.Row == 2)
            {
                bottomGroup = gr;
            }
            else if (pairGr.Row == 2)
            {
                bottomGroup = pairGr;
            }

            if (gr.Row == 0)
            {
                topGroup = gr;
            }
            else if (pairGr.Row == 0)
            {
                topGroup = pairGr;
            }

            Debug.Assert(topGroup == null, "topGroup==null");
            Debug.Assert(bottomGroup == null, "bottomGroup==null");
            SizeF sfift;
            SizeF pairShift;
            // bottomGroup.Rect
            if (gr.Type == bottomGroup.Type)
            {
                sfift = _bottomDestination - bottomGroup.Rect.Location;
                pairShift = _topDestination - topGroup.Rect.Location;

            }
            else
            {
                sfift = _topDestination - topGroup.Rect.Location;
                pairShift = _bottomDestination - bottomGroup.Rect.Location;
            }
            base.DoImpl();

            Next = new AssingGroupCommand(Type)
            {
                Next = new ShiftCommand(Type, sfift.Width, sfift.Height)
                {
                    New = new Command[] {
                        new ScaleOnStopCommand(Type),
                        new SelectUnitCommand(pairType)
                    {
                        Next = new AssingGroupCommand(pairType)
                        {
                            Next = new ShiftCommand(pairType,  pairShift.Width,pairShift.Height),
                            New= new Command[] {new ScaleOnStopCommand(pairType)}
                        }
                    }},

                }
            };
        }

        private static VehicleType GetPairType(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Fighter:
                    return VehicleType.Helicopter;
                case VehicleType.Helicopter:
                    return VehicleType.Fighter;
                case VehicleType.Ifv:
                    return VehicleType.Tank;
                case VehicleType.Tank:
                    return VehicleType.Ifv;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public class ScaleOnStopCommand : ScaleGroup
    {

        public ScaleOnStopCommand(VehicleType type) : base(type)
        {
        }

        public override bool CanAct()
        {
            return Vehiles.Where(e => e.Type == Type).All(e => !e.InMove);

        }

        protected override void DoImpl()
        {
            base.DoImpl();

            Move.Factor = 2;
            var center = Vehiles.GetGroupRect(Type).Center;
            Move.X = center.X;
            Move.Y = center.Y;
        }
    }

    #region Strategy 1
    public class FirstCommandStrategy1 : DeployCommand
    {



    }

    public class DeployCommand : SelectUnitCommand
    {
        private static int _count;

        protected override void DoImpl()
        {
            var gr = Strategy.StartMatrix.GetFreeGroup();

            if (gr == null)
                return;

            _count++;
            Type = gr.Type;
            base.DoImpl();

            Next = new AssingGroupCommand(Type)
            {
                Next = new MoveByTypeCommand(Type)
                {
                    Act = s =>
                    {
                        s.Commands.Add(new DeployScaleCommand1(Type));

                        if (_count < MyStrategy.GroupCount)
                            s.Commands.Add(new DeployCommand());
                    }
                }
            };
        }
    }

    public class DeployScaleCommand1 : ScaleGroup
    {
        private RectangleF _rectangle;
        public DeployScaleCommand1(VehicleType type) : base(type)
        {
        }

        public override bool CanAct()
        {
            _rectangle = Vehiles.GetGroupRect(Type);
            var minY = World.Height / 3;
            Log.Log("Wait Scale Type {0} {1} minY {2}", Type, _rectangle, minY);
            return _rectangle.Y >= minY;
        }

        protected override void DoImpl()
        {
            base.DoImpl();

            Move.Factor = 6;
            var center = _rectangle.Center;
            Move.X = center.X;
            Move.Y = center.Y;
        }
    }

    public class NuclearStriceCommand : Command
    {
        protected override ActionType ActionType => ActionType.TacticalNuclearStrike;
        private Veh[] seeVehiles;
        private Veh _vehicle;

        private static Circle circle = new Circle();

        public override bool CanAct()
        {
            base.CanAct();

            if (!Situation.CanNuclearStrike)
                return false;

            var rect = EnemyVehiles.GetRect();

            seeVehiles = Vehiles.Where(e => e.Durability > 50 && rect.Contains(e.X, e.Y)).ToArray();
            if (seeVehiles.IsEmpty()) return false;

            var maxStrice = 0;

            foreach (var seeVehile in seeVehiles)
            {
                circle.Init(seeVehile.X, seeVehile.Y);
                var inStriceEnemies = EnemyVehiles.Count(e => circle.InBound(e));

                var inStriceOurs = Vehiles.Count(e => circle.InBound(e));

                if (inStriceOurs < inStriceEnemies && inStriceOurs < 20 && inStriceEnemies > maxStrice)
                {
                    maxStrice = inStriceEnemies;
                    _vehicle = seeVehile;
                }
                if (maxStrice > 200)
                    break;
            }


            return seeVehiles.IsNotEmpty() && maxStrice > 50;
        }

        protected override void DoImpl()
        {
            base.DoImpl();

            Move.X = _vehicle.X;
            Move.Y = _vehicle.Y;
            Move.VehicleId = _vehicle.Id;

            Commands.Add(new NuclearStriceCommand());
        }
    }
    #endregion






}
