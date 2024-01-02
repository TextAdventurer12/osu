﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private const double wide_angle_multiplier = 1.0;
        private const double acute_angle_multiplier = 1.95;
        private const double slider_multiplier = 1.35;
        private const double velocity_change_multiplier = 0.75;
        private const double reaction_time = 150;

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static (double, double) EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, double strainDecayBase, double currentRhythm)
        {
            if (current.Index <= 2 || 
                current.BaseObject is Spinner || 
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return (0, 0);

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastObj1 = (OsuDifficultyHitObject)current.Previous(1);
            var osuLastObj2 = (OsuDifficultyHitObject)current.Previous(2);

            //////////////////////// CIRCLE SIZE /////////////////////////
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuLastObj0.Movement;
            var currTime = osuCurrObj.MovementTime;
            var prevTime = osuLastObj0.MovementTime;

            if (!withSliderTravelDistance)
            {
                currMovement = osuCurrObj.SliderlessMovement;
                prevMovement = osuLastObj0.SliderlessMovement;
                currTime = osuCurrObj.StrainTime;
                prevTime = osuLastObj0.StrainTime;
            }


            // Flow Stuff
            double flowDifficulty = linearDifficulty * currMovement.Length / currTime;

            // Nerf flow aim where circles overlap. Aim requirement is significantly lower in these cases.
            flowDifficulty *= currMovement.Length / (osuCurrObj.Radius * 2);

            // Snap Stuff
            // Reduce strain time by 25ms to account for stopping time.
            double snapDifficulty = Math.Max(linearDifficulty * ((osuCurrObj.Radius * 2 + currMovement.Length) / (osuCurrObj.StrainTime - 20)),
                                             linearDifficulty * currMovement.Length / currTime);

            double snapBuff = Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / (Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime) - 20);

            // Begin angle and weird rewards.
            double currVelocity = currMovement.Length / osuCurrObj.StrainTime;
            double prevVelocity = prevMovement.Length / osuLastObj0.StrainTime;

            double snapAngle = 0;
            double flowAngle = 0;

            if (osuCurrObj.Angle != null && osuLastObj0.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuLastObj0.Angle.Value;
 
                // We reward wide angles on snap.
                snapAngle = snapBuff * linearDifficulty * calculateAngleSpline(Math.Abs(currAngle), false) * Math.Min(Math.Min(currVelocity, prevVelocity), (currMovement + prevMovement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));

                // We reward for angle changes or the acuteness of the angle, whichever is higher. Possibly a case out there to reward both.
                flowAngle = linearDifficulty * Math.Pow(Math.Sin((currAngle - lastAngle) / 2), 2) * Math.Min(currVelocity, prevVelocity)
                                  + linearDifficulty * calculateAngleSpline(Math.Abs(currAngle), true) * Math.Min(Math.Min(currVelocity, prevVelocity), (currMovement - prevMovement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
            }

            double flowVelChange = linearDifficulty * Math.Max(0, Math.Min(Math.Abs(currVelocity - prevVelocity) - Math.Min(currVelocity, prevVelocity), Math.Max(osuCurrObj.Radius / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime), Math.Min(currVelocity, prevVelocity))));

            // double flowVelChange = linearDifficulty * Math.Abs(prevVelocity - currVelocity) * rhythmRatio;
            double snapVelChange = linearDifficulty * Math.Max(0, Math.Min(Math.Abs(prevVelocity - currVelocity) - Math.Min(currVelocity, prevVelocity), Math.Max(osuCurrObj.Radius / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime), Math.Min(currVelocity, prevVelocity))));

            snapDifficulty += snapVelChange + snapAngle;
            flowDifficulty += flowVelChange + flowAngle;

            // Arbitrary buff for high bpm snap because its hard.
            snapDifficulty *= Math.Pow(Math.Max(1, 100 / osuCurrObj.StrainTime), 0.75);

            // Apply balancing parameters.
            flowDifficulty = flowDifficulty * 1.4125;
            snapDifficulty = snapDifficulty * 0.8625; 
        
            // Apply small CS buff.
            snapDifficulty *= Math.Sqrt(linearDifficulty);
            
            // Slider stuff.
            double sustainedSliderStrain = 0.0;

            if (osuCurrObj.SliderSubObjects.Count != 0)
                sustainedSliderStrain = calculateSustainedSliderStrain(osuCurrObj, strainDecayBase, withSliderTravelDistance);
            
            // Apply slider strain with constant adjustment
            flowDifficulty += 2.0 * sustainedSliderStrain;
            snapDifficulty += 2.0 * sustainedSliderStrain;

            // AR buff for aim.
            double arBuff = (1.0 + 0.05 * Math.Max(0.0, 400.0 - osuCurrObj.ApproachRateTime) / 100.0);

            return (arBuff * flowDifficulty, arBuff * snapDifficulty);
        }

        private static double calculateSustainedSliderStrain(OsuDifficultyHitObject osuCurrObj, double strainDecayBase, bool withSliderTravelDistance)
        {
            int index = 1;

            double sliderRadius = 2.4 * osuCurrObj.Radius;
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var previousHistoryVector = new Vector2(0,0);
            var historyVector = new Vector2(0,0);
            var priorMinimalPos = new Vector2(0,0);
            double historyTime = 0;
            double historyDistance = 0;

            double peakStrain = 0;
            double currentStrain = 0;

            foreach (var subObject in osuCurrObj.SliderSubObjects)
            {
                if (index == osuCurrObj.SliderSubObjects.Count && !withSliderTravelDistance)
                    break;

                double noteStrain = 0;

                // if (index == 0 && osuCurrObj.SliderSubObjects.Count > 1)
                //     noteStrain = Math.Max(0, linearDifficulty * subObject.Movement.Length - 2 * osuCurrObj.Radius) / subObject.StrainTime;

                historyVector += subObject.Movement;
                historyTime += subObject.StrainTime;
                historyDistance += subObject.Movement.Length;
                
                if ((historyVector - priorMinimalPos).Length > sliderRadius)
                {
                    double angleBonus = Math.Min(Math.Min(previousHistoryVector.Length, historyVector.Length), Math.Min((previousHistoryVector - historyVector).Length, (previousHistoryVector + historyVector).Length));

                    noteStrain += linearDifficulty * (historyDistance + angleBonus - sliderRadius) / historyTime;

                    previousHistoryVector = historyVector;
                    priorMinimalPos = Vector2.Multiply(historyVector, (float) - sliderRadius / historyVector.Length);
                    historyVector = new Vector2(0,0);
                    historyTime = 0;
                    historyDistance = 0;
                }

                currentStrain *= Math.Pow(strainDecayBase, subObject.StrainTime / 1000.0); // TODO bug here using strainTime.
                currentStrain += noteStrain;
                peakStrain = Math.Max(peakStrain, currentStrain);

                index += 1;
            }

            if (historyTime > 0 && withSliderTravelDistance)
                currentStrain += Math.Max(0, linearDifficulty * Math.Max(0, historyDistance - 2 * osuCurrObj.Radius) / historyTime);

            return Math.Max(currentStrain, peakStrain);
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
            {
                if (angle == Math.PI) 
                    return 0;
                return (Math.PI - angle) / Math.Sqrt(2 * (1 - Math.Cos(Math.PI - angle))) - 1;
            }
            // return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 6, 2 * Math.PI / 3.0) - Math.PI / 6), 2.0);

            // angle = Math.Abs(angle);
            // if (reversed)
            //     return 1 - Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 4.0, 3 * Math.PI / 4.0) - Math.PI / 4), 2.0);

            // return Math.Pow(Math.Sin(Math.Clamp(2 * angle, Math.PI / 2.0, Math.PI) - Math.PI / 2), 2.0);

            return Math.Pow(Math.Sin(Math.Min(1.2 * angle - Math.PI / 4.0, (2.0 / 3.0) * Math.PI - Math.PI / 4.0)), 2);

            // return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 4.0, (3.0 / 4.0) * Math.PI) - Math.PI / 4), 2.0);

            // angle = Math.Abs(angle);
            // if (reversed)
            //     return 1 - Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 3.0, 5 * Math.PI / 6.0) - Math.PI / 3), 2.0);

            // return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 3.0, 5 * Math.PI / 6.0) - Math.PI / 3), 2.0);
        }
    }
}
