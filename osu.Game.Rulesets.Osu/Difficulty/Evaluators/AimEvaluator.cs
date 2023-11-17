// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance, double strainDecayBase, double currentRhythm)
        {
            if (current.Index <= 2 || 
                current.BaseObject is Spinner || 
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuLastObj1 = (OsuDifficultyHitObject)current.Previous(1);
            var osuLastObj2 = (OsuDifficultyHitObject)current.Previous(2);

            double aimStrain = 0;

            //////////////////////// CIRCLE SIZE /////////////////////////
            double circleArea = Math.PI * Math.Pow(osuCurrObj.Radius, 2);
            double areaDifficulty = 3200.0 / circleArea;
            double linearDifficulty = 32.0 / osuCurrObj.Radius;
            // double circleArea = Math.Pow(osuCurrObj.Radius, 1.5);
            // double areaDifficulty = 180 / circleArea;

            areaDifficulty = Math.Sqrt(areaDifficulty * linearDifficulty);
            // linearDifficulty = areaDifficulty;

            double flowDifficulty = areaDifficulty * osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            flowDifficulty *= Math.Min(1, osuCurrObj.Movement.Length / (osuCurrObj.Radius * 2));
            // flowDifficulty *= osuCurrObj.Movement.Length / (osuCurrObj.Radius * 3);
            // double snapDifficulty = Math.Max(100 * areaDifficulty * osuCurrObj.Movement.Length / Math.Pow(Math.Min(osuCurrObj.ApproachRateTime - reaction_time, osuCurrObj.StrainTime), 2), 
            //                                  0);//linearDifficulty * osuCurrObj.Movement.Length / Math.Min(osuCurrObj.ApproachRateTime - reaction_time, osuCurrObj.StrainTime));

            double snapDifficulty = areaDifficulty * osuCurrObj.Movement.Length / Math.Max(30, (osuCurrObj.StrainTime - 30));

            // snapDifficulty *= Math.Max(1, 100 / osuCurrObj.StrainTime);

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevVelocity = osuLastObj0.Movement.Length / osuLastObj0.StrainTime;

            // double currVelocity = Math.Max(100 * areaDifficulty * osuCurrObj.Movement.Length / Math.Pow(osuCurrObj.StrainTime, 2), linearDifficulty * osuCurrObj.Movement.Length / osuCurrObj.StrainTime);
            // double prevVelocity = Math.Max(100 * areaDifficulty * osuLastObj0.Movement.Length / Math.Pow(osuLastObj0.StrainTime, 2), linearDifficulty * osuLastObj0.Movement.Length / osuLastObj0.StrainTime);


            double snapFlowDifficulty = areaDifficulty * osuLastObj0.Movement.Length / (osuLastObj0.StrainTime - 20)
                                            + linearDifficulty * osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            double flowSnapDifficulty = areaDifficulty * osuLastObj0.Movement.Length / (osuLastObj0.StrainTime - 20)
                                            + linearDifficulty * osuLastObj0.Movement.Length / osuLastObj0.StrainTime;

            double rhythmRatio = Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime);

            if (0.75 > rhythmRatio)
                rhythmRatio = 0;

            if (osuCurrObj.Angle != null && osuLastObj0.Angle != null)
            {
                double angle = osuCurrObj.Angle.Value;
                double lastAngle = osuLastObj0.Angle.Value;
                flowDifficulty += linearDifficulty * calculateAngleSpline((angle + lastAngle) / 2, true) * Math.Min(Math.Min(currVelocity, prevVelocity), (osuCurrObj.Movement - osuLastObj0.Movement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
                snapDifficulty += linearDifficulty * calculateAngleSpline((angle + lastAngle) / 2, false) * Math.Min(Math.Min(currVelocity, prevVelocity), (osuCurrObj.Movement + osuLastObj0.Movement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));

                flowDifficulty +=  linearDifficulty * calculateAngleSpline(Math.PI / 4 + Math.Min(Math.PI / 2, Math.Abs(lastAngle - angle)), false) * Math.Min(Math.Min(currVelocity, prevVelocity), (osuCurrObj.Movement + osuLastObj0.Movement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime)) * rhythmRatio;
                // flowSnapDifficulty += linearDifficulty * calculateAngleSpline(angle, false) * Math.Min(Math.Min(currVelocity, prevVelocity), (osuCurrObj.Movement + osuLastObj0.Movement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
                // snapFlowDifficulty += linearDifficulty * calculateAngleSpline(angle, true) * Math.Min(Math.Min(currVelocity, prevVelocity), (osuCurrObj.Movement - osuLastObj0.Movement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
            }

            flowDifficulty += linearDifficulty * Math.Abs(currVelocity - prevVelocity) * rhythmRatio;
            snapDifficulty += linearDifficulty * Math.Max(0, Math.Min(Math.Abs(currVelocity - prevVelocity) - Math.Min(currVelocity, prevVelocity), Math.Min(currVelocity, prevVelocity))) * rhythmRatio;

            aimStrain = 1000000000;//areaDifficulty * (prevVelocity + currVelocity);//Math.Min(snapFlowDifficulty, flowSnapDifficulty);// * Math.Sqrt(Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));

            // aimStrai= n = Math.Min(aimStrain, currVelocity + prevVelocity);                  
            aimStrain = Math.Min(aimStrain, Math.Min(0.875 * snapDifficulty, 1.325 * flowDifficulty)); 
            aimStrain = Math.Max(aimStrain, (aimStrain - areaDifficulty * 2.4 * osuCurrObj.Radius / Math.Min(osuCurrObj.MovementTime, osuLastObj0.MovementTime)) * (osuCurrObj.StrainTime / osuCurrObj.MovementTime));   
        
            // aimStrain = Math.Min(snapDifficulty, 0.9 * flowDifficulty);
            // * (osuCurrObj.StrainTime / osuCurrObj.MovementTime);
            // // 3200 is approximately the area of a CS 5 circle.
            // double jumpDifficulty = 0;
            // double flowDifficulty = 0;

            // if (osuCurrObj.Angle != null && osuLastObj0.Angle != null)
            // {
            //     double angle = (osuCurrObj.Angle.Value + osuLastObj0.Angle.Value) / 2;
            //     jumpDifficulty = 10 * Math.Sqrt(areaDifficulty * linearDifficulty) * 
            //                                     (Math.Max((osuCurrObj.Movement.Length - 2.4 * osuCurrObj.Radius) / Math.Pow(osuCurrObj.MovementTime, 1.5),
            //                                                osuCurrObj.Movement.Length / Math.Pow(osuCurrObj.StrainTime, 1.5))
            //                                      + calculateAngleSpline(angle, false)
            //                                       * Math.Max((osuCurrObj.Movement + osuLastObj0.Movement).Length / Math.Pow(osuCurrObj.StrainTime + osuLastObj0.StrainTime, 1.5),
            //                                         ((osuCurrObj.Movement + osuLastObj0.Movement).Length - 4.8 * osuCurrObj.Radius) / Math.Pow((osuCurrObj.MovementTime + osuLastObj0.MovementTime) / 2, 1.5)));


            //     // jumpDifficulty = Math.Max(jumpDifficulty, Math.Sqrt(areaDifficulty * linearDifficulty)
            //     //                  * Math.Max((osuCurrObj.Movement.Length - 2.4 * osuCurrObj.Radius)  / osuCurrObj.MovementTime, 
            //     //                              osuCurrObj.Movement.Length / osuCurrObj.StrainTime));

            //     // jumpDifficulty += 75 * Math.Sqrt(areaDifficulty * linearDifficulty) * 
            //     //                     Math.Min(osuCurrObj.Radius * 4, Math.Max(0, osuCurrObj.Movement.Length - osuCurrObj.Radius)) / Math.Pow(osuCurrObj.MovementTime, 2);

            //     flowDifficulty = 1.0 * Math.Sqrt(areaDifficulty * linearDifficulty) * 
            //     (osuCurrObj.Movement.Length / osuCurrObj.StrainTime
            //      + calculateAngleSpline(angle, true) * (osuCurrObj.Movement - osuLastObj0.Movement).Length / ((osuCurrObj.StrainTime + osuLastObj0.StrainTime) / 2));
            // }

            // double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            // double prevVelocity = osuLastObj0.Movement.Length / osuLastObj0.StrainTime;
            
            // flowDifficulty += Math.Sqrt(areaDifficulty * linearDifficulty)
            //                  * Math.Sqrt(Math.Max(0, Math.Min(currVelocity, prevVelocity) - osuCurrObj.Radius / Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime))
            //                              * Math.Abs(currVelocity - prevVelocity));

            //                     // * (Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
            // // flowDifficulty *= Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime);
            // // jumpDifficulty *= Math.Min(osuCurrObj.StrainTime, osuLastObj0.StrainTime) / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime);

            // // double jumpDifficulty = 65 * areaDifficulty * (osuCurrObj.Movement.Length / Math.Pow(osuCurrObj.MovementTime, 2) + calculateAngleSpline(osuCurrObj.Angle) * (osuCurrObj.Movement + osuLastObj0.Movement).Length / Math.Pow((osuCurrObj.MovementTime + osuLastObj0.MovementTime) / 2, 2));
            // // double flowDifficulty = 0.525 * Math.Sqrt(areaDifficulty * linearDifficulty) * (osuCurrObj.Movement.Length / osuCurrObj.StrainTime + (osuCurrObj.Movement - osuLastObj0.Movement).Length / ((osuCurrObj.StrainTime + osuLastObj0.StrainTime) / 2));
            // // double transitionDifficulty
            // // jumpDifficulty *= Math.Min(osuCurrObj.MovementTime, osuLastObj0.MovementTime) / Math.Min(osuCurrObj.MovementTime, osuLastObj0.MovementTime);
            // // flowDifficulty *= Math.Min(osuCurrObj.MovementTime, osuLastObj0.MovementTime) / Math.Min(osuCurrObj.MovementTime, osuLastObj0.MovementTime);
            // aimStrain += Math.Min(jumpDifficulty, flowDifficulty);
            // // aimStrain += 9 * Math.Sqrt(areaDifficulty * linearDifficulty) * (osuCurrObj.Movement.Length / Math.Pow(osuCurrObj.MovementTime, 1.5));
            // // aimStrain += flowDifficulty;

            // // aimStrain = Math.Min(aimStrain, 1.5 * Math.Max(osuCurrObj.Movement.Length / osuCurrObj.StrainTime, osuLastObj0.Movement.Length / osuLastObj0.StrainTime));
            // if (Math.Abs(osuCurrObj.StrainTime - osuLastObj0.StrainTime) > 10)
            //     aimStrain = currentRhythm * Math.Sqrt(areaDifficulty * linearDifficulty) * osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            //////////////////////// VECTOR DEFINITIONS /////////////////////////
            double sustainedSliderStrain = 0.0;

            if (osuCurrObj.SliderSubObjects.Count != 0 && withSliderTravelDistance)
                sustainedSliderStrain = calculateSustainedSliderStrain(osuCurrObj, strainDecayBase, withSliderTravelDistance);
            
            aimStrain += sustainedSliderStrain;

            // double arBuff = (1.0 + 0.0 * Math.Max(0.0, 400.0 - osuCurrObj.ApproachRateTime) / 100.0);

            return aimStrain;
        }

        // private static double calculatePositionalAreaDifficulty(Vector2 movement, double circleDiameter)
        // {
        //     if movement.length > circleDiameter
        // }

        private static double calculateSustainedSliderStrain(OsuDifficultyHitObject osuCurrObj, double strainDecayBase, bool withSliderTravelDistance)
        {
            int index = 0;

            double sliderRadius = 2.4 * osuCurrObj.Radius;
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var historyVector = new Vector2(0,0);
            double historyTime = 0;
            double historyDistance = 0;

            double peakStrain = 0;
            double currentStrain = 0;

            foreach (var subObject in osuCurrObj.SliderSubObjects)
            {
                if (index == osuCurrObj.SliderSubObjects.Count && !withSliderTravelDistance)
                    break;
                // Console.WriteLine(index);
                // Console.WriteLine(subObject.Movement.Length);
                // Console.WriteLine(subObject.StrainTime);

                double noteStrain = 0;

                if (index == 0 && osuCurrObj.SliderSubObjects.Count > 1)
                    noteStrain = Math.Max(0, linearDifficulty * subObject.Movement.Length) / subObject.StrainTime;

                historyVector += subObject.Movement;
                historyTime += subObject.StrainTime;
                historyDistance += subObject.Movement.Length;
                
                if (historyVector.Length > sliderRadius * 2.0)
                {
                    noteStrain += linearDifficulty * historyDistance / historyTime;

                    historyVector = new Vector2(0,0);
                    historyTime = 0;
                    historyDistance = 0;
                }
                // else if (index == 0 && index != osuCurrObj.SliderSubObjects.Count)
                // {
                //     // Want to try to calculate the held position difficulty.
                //     peakStrain = Math.Max(peakStrain, Math.Sqrt(areaDifficulty * (3200 / (sliderRadius * (2.0 * sliderRadius - subObject.Movement.Length)))) / historyTime);
                //     // staticStrain = sliderRadius * 2 * (sliderRadius * 2 - subObject.Movement.Length) / historyTime;
                // }

                currentStrain *= Math.Pow(strainDecayBase, subObject.StrainTime / 1000.0); // TODO bug here using strainTime.
                currentStrain += noteStrain;
                // peakStrain = Math.Max(peakStrain, currStrain);
                index += 1;

                // Console.WriteLine(currentStrain);
            }
            // Console.WriteLine("finished slider");

            if (historyTime > 0 && withSliderTravelDistance)
            {
                if (osuCurrObj.SliderSubObjects.Count > 1)
                    currentStrain += Math.Max(0, linearDifficulty * Math.Max(0, historyVector.Length) / historyTime);
                else
                    currentStrain += Math.Max(0, linearDifficulty * Math.Max(0, historyVector.Length - 2 * osuCurrObj.Radius) / historyTime);
            }

            return Math.Max(currentStrain, peakStrain);
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            angle = Math.Abs(angle);
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 3.0, 5 * Math.PI / 6.0) - Math.PI / 3), 2.0);

            // return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 6, 2 * Math.PI / 3.0) - Math.PI / 6), 2.0);

            // angle = Math.Abs(angle);
            // if (reversed)
            //     return 1 - Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 4.0, 3 * Math.PI / 4.0) - Math.PI / 4), 2.0);

            return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 4.0, 3 * Math.PI / 4.0) - Math.PI / 4), 2.0);


            // angle = Math.Abs(angle);
            // if (reversed)
            //     return 1 - Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 3.0, 5 * Math.PI / 6.0) - Math.PI / 3), 2.0);

            // return Math.Pow(Math.Sin(Math.Clamp(angle, Math.PI / 3.0, 5 * Math.PI / 6.0) - Math.PI / 3), 2.0);
        }
    }
}
