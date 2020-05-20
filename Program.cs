using System;
using Accord.Statistics.Distributions.Univariate;
using System.Collections.Generic;
using Kezyma.EloRating;
using System.Linq;

namespace mtga_open
{
    class Program
    {
        static void Main(string[] args)
        {
            int numPlayers = 0;
            int numReentries = 0;

            try
            {
                numPlayers = int.Parse(args[0]);
                numReentries = int.Parse(args[1]);
            }
            catch
            {
                Console.WriteLine("Please use the format \"dotnet run [number of players] [number of reentries] [optional: elo standard deviation]\"");
                Environment.Exit(1);
            }

            double eloDistribution = 0;
            if (args.Length > 2 && double.TryParse(args[2], out var stdDev))
            {
                eloDistribution = stdDev;
            }
            else
            {
                // I picked the "all limited players" as the most likely representation of magic luck vs skill
                // https://www.mtgcommunityreview.com/single-post/2018/06/12/Luck-Skill-and-Magic
                eloDistribution = 83;
            }
            Run(numPlayers, numReentries, eloDistribution);
        }

        static void Run(int numPlayers, int numReentries, double eloDistribution)
        {
            // This doesn't really matter but I picked from the same source as my default distribution
            // https://www.mtgcommunityreview.com/single-post/2018/06/12/Luck-Skill-and-Magic
            double eloMean = 1678.632;
            // I'm not actually going to adjust anyone's elo as they go - I'm using it as a representation of "true" skill
            var playerDistribution = new NormalDistribution(eloMean, eloDistribution);
            var players = new List<Player>();
            for (var index = 0; index < numPlayers; index++)
            {
                var player = new Player();
                player.Elo = (decimal)playerDistribution.Generate();
                player.GemsSpent = 4000;
                players.Add(player);
            }

            var dayTwoPlayers = RunDayOne(players, numReentries);
            foreach (var player in dayTwoPlayers)
            {
                player.CurrentWins = 0;
                player.CurrentLosses = 0;
            }
            RunDayTwo(dayTwoPlayers);

            var dayOneAvgElo = players.Average(p => p.Elo);
            var dayTwoAvgElo = dayTwoPlayers.Average(p => p.Elo);
            var dayTwoWinPercent = EloCalculator.PredictResult(dayTwoAvgElo, (decimal)eloMean)[0]*100;
            Console.WriteLine($"Day one average Elo: {dayOneAvgElo}.");
            Console.WriteLine($"Day two average Elo: {dayTwoAvgElo} ({dayTwoWinPercent,3:0.0}% win percent).");

            var numBuckets = 100;
            var bucketSize = numPlayers / numBuckets;
            if (numPlayers % numBuckets != 0)
            {
                Console.WriteLine("Warning - please use a multiple of " + numBuckets + " for more accurate top end performance");
            }

            var eloSorted = players.OrderBy(p => p.Elo).ToList();
            var skip = 0;
            while (skip + bucketSize <= eloSorted.Count)
            {
                var nextBucket = eloSorted.Skip(skip).Take(bucketSize).ToList();
                var minElo = (int)nextBucket.Min(p => p.Elo);
                var maxElo = (int)nextBucket.Max(p => p.Elo);
                var avgElo = (int)nextBucket.Average(p => p.Elo);
                var avgGems = nextBucket.Average(p => p.GemsWon - p.GemsSpent);
                var avgDayOneWins = nextBucket.Average(p => p.DayOneWins);
                var avgDayOneLosses = nextBucket.Average(p => p.DayOneLosses);
                var avgDayTwoWins = nextBucket.Average(p => p.DayTwoWins);
                var avgDayTwoLosses = nextBucket.Average(p => p.DayTwoLosses);
                var winPercentage = EloCalculator.PredictResult(avgElo, (decimal)eloMean)[0]*100;
                var bucketNumber = skip/bucketSize+1;

                Console.WriteLine($"{avgElo} ({minElo}-{maxElo}) ({winPercentage,3:0.0}% game win%, bucket #{bucketNumber}): {avgGems} gems, " +
                                  $"{avgDayOneWins,3:0.0}-{avgDayOneLosses,3:0.0} on day one, " +
                                  $"{avgDayTwoWins,3:0.0}-{avgDayTwoLosses,3:0.0} on day two.");
                skip += bucketSize;
            }
        }

        public static void RunDayTwo(List<Player> players)
        {
            var dayTwoPlayers = new List<Player>(players);
            var roundNumber = 0;

            while (dayTwoPlayers.Count > 1)
            {
                Shuffle(dayTwoPlayers);
                for (var index = 0; index < dayTwoPlayers.Count - 1; index += 2)
                {
                    var playerOne = dayTwoPlayers[index];
                    var playerTwo = dayTwoPlayers[index + 1];

                    var playerOneGameWinChance = EloCalculator.PredictResult(playerOne.Elo, playerTwo.Elo)[0];
                    var playerOneMatchWinChance = playerOneGameWinChance * playerOneGameWinChance * (3 - 2 * playerOneGameWinChance);
                    bool playerOneWins = playerOneMatchWinChance >= (decimal)rng.NextDouble();
                    if (playerOneWins)
                    {
                        playerOne.DayTwoWins++;
                        playerTwo.DayTwoLosses++;
                    }
                    else
                    {
                        playerTwo.DayTwoWins++;
                        playerOne.DayTwoLosses++;
                    }
                }

                for (var index = dayTwoPlayers.Count - 1; index >= 0; index--)
                {
                    var player = dayTwoPlayers[index];
                    if (player.DayTwoLosses == 2)
                    {
                        var gemsWon = 0;
                        switch (player.DayTwoWins)
                        {
                            case 1:
                            case 2:
                            case 3:
                                gemsWon = player.DayTwoWins * 2000;
                                break;
                            case 4:
                            case 5:
                                gemsWon = (player.DayTwoWins - 3) * 10000;
                                break;
                            case 6:
                                gemsWon = 200000;
                                break;
                        }
                        player.GemsWon += gemsWon;
                        dayTwoPlayers.RemoveAt(index);
                    }
                    else if (player.DayTwoWins == 7) // shouldn't really need the else but if there's a bug it's less bad this way
                    {
                        player.GemsWon += 400000;
                        dayTwoPlayers.RemoveAt(index);
                    }
                }
                roundNumber++;
                Console.WriteLine("After round " + roundNumber + " of Day 2 - " + dayTwoPlayers.Count +
                                  " remaining players.");


            }
        }

        public static List<Player> RunDayOne(List<Player> players, int numReentries)
        {
            var dayOnePlayers = new List<Player>(players);
            var dayTwoPlayers = new List<Player>();
            var roundNumber = 0;
            // need to avoid getting stuck with an odd player out
            // probably just fail them out
            while (dayOnePlayers.Count > 1)
            {
                // random match making until we know better
                Shuffle(dayOnePlayers);

                for (var index = 0; index < dayOnePlayers.Count - 1; index += 2)
                {
                    var playerOne = dayOnePlayers[index];
                    var playerTwo = dayOnePlayers[index + 1];
                    // yeah yeah yeah decimals aren't doubles blah blah blah
                    bool playerOneWins = EloCalculator.PredictResult(playerOne.Elo, playerTwo.Elo)[0] >= (decimal)rng.NextDouble();
                    if (playerOneWins)
                    {
                        playerOne.CurrentWins++;
                        playerOne.DayOneWins++;
                        playerTwo.CurrentLosses++;
                        playerTwo.DayOneLosses++;
                    }
                    else
                    {
                        playerTwo.CurrentWins++;
                        playerTwo.DayOneWins++;
                        playerOne.CurrentLosses++;
                        playerOne.DayOneLosses++;
                    }
                }

                for (var index = dayOnePlayers.Count - 1; index >= 0; index--)
                {
                    var player = dayOnePlayers[index];
                    if (player.CurrentLosses == 3)
                    {
                        player.GemsWon += Math.Max(0, 400 * (player.CurrentWins - 2));
                        if (player.Run <= numReentries)
                        {
                            // Re-enter the tournament
                            player.Run++;
                            player.CurrentWins = 0;
                            player.CurrentLosses = 0;
                            player.GemsSpent += 4000;
                        }
                        else
                        {
                            // you're out!
                            dayOnePlayers.RemoveAt(index);
                        }
                    }
                    else if (player.CurrentWins == 7) // shouldn't really need the else but if there's a bug it's less bad this way
                    {
                        dayTwoPlayers.Add(player);
                        dayOnePlayers.RemoveAt(index);
                    }
                }
                roundNumber++;
                Console.WriteLine("After round " + roundNumber + " of Day 1 - " + dayOnePlayers.Count +
                                  " remaining players, and " + dayTwoPlayers.Count + " day two players.");

            }
            return dayTwoPlayers;
        }

        private static Random rng = new Random();

        public static void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    public class Player
    {
        public decimal Elo = 1500;
        public int CurrentWins = 0;
        public int DayOneWins = 0;
        public int DayTwoWins = 0;
        public int CurrentLosses = 0;
        public int DayOneLosses = 0;
        public int DayTwoLosses = 0;
        public int Run = 1;
        public int GemsSpent = 0;
        public int GemsWon = 0;
    }
}
