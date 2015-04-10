using System;
using System.Collections.Generic;
using System.Text;
using Moserware.Skills.TrueSkill;
using IW4MAdmin;


namespace Moserware
{
    class TrueSkill
    {
        public TrueSkill()
        {
            calculator = new TwoPlayerTrueSkillCalculator();
            gInfo = Skills.GameInfo.DefaultGameInfo;
        }

        public void updateNewSkill(Player P1, Player P2)
        {
            var player1 = new Skills.Player(P1.getDBID());
            var player2 = new Skills.Player(P2.getDBID());

            var team1 = new Skills.Team(player1, P1.stats.Rating);
            var team2 = new Skills.Team(player2, P2.stats.Rating);

            var newRatings = calculator.CalculateNewRatings(gInfo, Skills.Teams.Concat(team1, team2), 1, 2);

            P1.stats.Rating = newRatings[player1];
            P2.stats.Rating = newRatings[player2];

            P1.stats.Skill = Math.Round(P1.stats.Rating.ConservativeRating, 3)*10;
            P2.stats.Skill = Math.Round(P2.stats.Rating.ConservativeRating, 3)*10;
        }
    
        private Skills.SkillCalculator calculator;
        public Skills.GameInfo gInfo;
    }
}