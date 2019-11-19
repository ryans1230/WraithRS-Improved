using CitizenFX.Core;
using CitizenFX.Core.Native;
using CitizenFX.Core.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dojrp.Fivem.WraithRS.Client
{
    public class Main : BaseScript
    {
        protected bool radarEnabled, hidden, frontFast, rearFast, locked, debugging, calibrated, lidarShown = false;
        protected RadarInfo radarInfo = new RadarInfo();

        public Main()
        {
            API.SetNuiFocus(false, false);

        }

        [Command("radar")]
        private async void RadarCommand(int src, List<object> args, string raw)
        {

            if (args.Count == 0)
                return;

            string cmd = args[0].ToString();

            if (cmd == "debug")
            {
                debugging = !debugging;
                TriggerEvent("wk:radarRC");
            } else if (cmd == "calibrate")
            {
                Screen.ShowNotification("~b~Calibrating radar. . . .");
                await Delay((new Random().Next(1000, 2500)));

                Screen.ShowNotification("~b~Radar has been calibrated!");
                calibrated = true;
            }

        }

        [Tick]
        private async Task OnTick()
        {
            ManageVehicleRadar();
            await Delay(100);
        }

        [Tick]
        private async Task SecondaryTick()
        {
            ManageLidar();
            Ped player = Game.PlayerPed;
            bool inVeh = player.IsSittingInVehicle();
            Vehicle veh = player.CurrentVehicle;

            if (inVeh)
            {
                // LCtrl is pressed and Z has just been pressed
                if (Game.IsControlPressed(1, Control.Duck) && Game.IsControlJustPressed(1, Control.MultiplayerInfo) && (veh.Driver == player || veh.GetPedOnSeat(VehicleSeat.Passenger) == player))
                    TriggerEvent("wk:radarRC");

                if (!Game.IsControlPressed(1, Control.Duck) && Game.IsControlJustPressed(1, Control.MultiplayerInfo) && Game.CurrentInputMode == InputMode.MouseAndKeyboard)
                {
                    TriggerEvent("wk:resetFast");
                }
            }

            if (((!inVeh || (inVeh && veh.Exists() && veh.ClassType != VehicleClass.Emergency)) && radarEnabled && !hidden) || (API.IsPauseMenuActive() && radarEnabled))
            {
                hidden = true;
                API.SendNuiMessage(Json.Stringify(new
                {
                    hideradar = true
                }));
            }
            else if (inVeh && veh.Exists() && veh.ClassType == VehicleClass.Emergency && radarEnabled && hidden)
            {
                hidden = false;
                API.SendNuiMessage(Json.Stringify(new
                {
                    hideradar = false
                }));
            }

            if (locked)
            {
                Game.DisableControlThisFrame(0, Control.LookLeftRight);
                Game.DisableControlThisFrame(0, Control.LookUpDown);
                Game.DisableControlThisFrame(0, Control.Attack);
                API.DisablePlayerFiring(player.Handle, true);
                Game.DisableControlThisFrame(0, Control.MeleeAttackAlternate);
                Game.DisableControlThisFrame(0, Control.VehicleMouseControlOverride);

                API.SetPauseMenuActive(false);

            }

            await Task.FromResult(0);
        }

        [Tick]
        private async Task ResourceNameFix()
        {
            await Delay(1000);
            string rName = API.GetCurrentResourceName();
            API.SendNuiMessage(Json.Stringify(new
            {
                resourcename = rName
            }));
        }

        private double round(double num)
        {
            return Math.Round(num);
        }

        private double oppang(double ang)
        {
            return (ang + 180) % 360;
        }

        private string FormatSpeed(double speed)
        {
            if (speed < 0)
                speed = 0;
            return speed.ToString("000");
        }

        private int GetVehicleInDirectionSphere(int entFrom, Vector3 coordFrom, Vector3 coordTo, float radius = 4f)
        {
            int rayhandle = API.StartShapeTestCapsule(coordFrom.X, coordFrom.Y, coordFrom.Z, coordTo.X, coordTo.Y, coordTo.Z, radius, 10, entFrom, 7);
            bool dump = false;
            Vector3 trash = new Vector3(), trash2 = new Vector3();
            int car = 0;
            _ = API.GetShapeTestResult(rayhandle, ref dump, ref trash, ref trash2, ref car);

            return car;
        }

        private int IsEntityInMyHeading(double myAng, double tarAng, int range)
        {
            double rangeStartFront = myAng - (range / 2);
            double rangeEndFront = myAng + (range / 2);

            double opp = oppang(myAng);

            double rangeStartBack = opp - (range / 2);
            double rangeEndBack = opp + (range / 2);

            if (tarAng > rangeStartFront && tarAng < rangeEndFront)
                return 1;
            if (tarAng > rangeStartBack && tarAng < rangeEndBack)
                return 0;
            return -1;
        }

        [EventHandler("wk:toggleMenuControlLock")]
        private void ToggleMenuControlLock(bool lck)
        {
            locked = lck;
        }

        [EventHandler("wk:toggleRadar")]
        private void ToggleRadar()
        {
            Ped player = Game.PlayerPed;

            if (player.IsSittingInVehicle())
            {
                Vehicle veh = player.CurrentVehicle;

                if (veh.ClassType == VehicleClass.Emergency)
                {
                    radarEnabled = !radarEnabled;

                    if (radarEnabled)
                        Screen.ShowNotification("~b~Radar enabled.");
                    else
                        Screen.ShowNotification("~b~Radar disabled");

                    ResetFrontAntenna();
                    ResetRearAntenna();

                    API.SendNuiMessage(Json.Stringify(new
                    {
                        toggleradar = true,
                        fwdxmit = radarInfo.fwdXmit,
                        fwdmode = radarInfo.fwdMode,
                        fwdfast = radarInfo.fwdFast,
                        fwdspeed = radarInfo.fwdSpeed,
                        bwdxmit = radarInfo.bwdXmit,
                        bwdmode = radarInfo.bwdMode,
                        bwdfast = radarInfo.bwdFast,
                        bwdspeed = radarInfo.bwdSpeed,
                        patrolspeed = radarInfo.patrolSpeed
                    }));
                }
                else
                {
                    Screen.ShowNotification("~r~You must be in a police vehicle.");
                }
            }
            else
            {
                Screen.ShowNotification("~r~You must be in a vehicle.");
            }
        }

        [EventHandler("wk:changeRadarLimit")]
        private void ChangeRadarLimit(int speed)
        {
            radarInfo.fastLimit = speed;
        }

        [EventHandler("wk:radarRC")]
        private async void RadarRC()
        {
            await Delay(10);
            TriggerEvent("wk:toggleMenuControllerLock", true);
            API.SendNuiMessage(Json.Stringify(new
            {
                toggleradarrc = true
            }));
            API.SetNuiFocus(true, true);
        }

        [EventHandler("wk:resetFast")]
        private async void ResetFast()
        {
            await Delay(10);
            ResetFrontFast();
            ResetRearFast();
            ResetLidarFast();
        }

        private async void Radar_SetLimit()
        {
            API.DisplayOnscreenKeyboard(0, "Enter a Fast Limit", "Enter a Fast Limit", radarInfo.fastLimit.ToString(), "", "", "", 3);

            while (true)
            {
                if (API.UpdateOnscreenKeyboard() == 1)
                {
                    string speedStr = API.GetOnscreenKeyboardResult();

                    if (speedStr.Length > 0)
                    {
                        int speed = int.Parse(speedStr);

                        if (speed > 1 && speed < 999)
                            TriggerEvent("wk:changeRadarLimit", speed);

                        break;
                    }
                    else
                    {
                        API.DisplayOnscreenKeyboard(0, "Enter a Fast Limit", "", radarInfo.fastLimit.ToString(), "", "", "", 3);
                    }
                }
                else if (API.UpdateOnscreenKeyboard() == 2)
                {
                    break;
                }
                await Delay(10);
            }
        }

        private void ResetFrontAntenna()
        {
            if (radarInfo.fwdXmit)
            {
                radarInfo.fwdSpeed = "000";
                radarInfo.fwdFast = "000";
            }
            else
            {
                radarInfo.fwdSpeed = "OFF";
                radarInfo.fwdFast = "   ";
            }

            radarInfo.fwdDir = -1;
            radarInfo.fwdFastLocked = false;
            radarInfo.fwdFastSpeed = 0;
        }

        private void ResetRearAntenna()
        {
            if (radarInfo.bwdXmit)
            {
                radarInfo.bwdSpeed = "000";
                radarInfo.bwdFast = "000";
            }
            else
            {
                radarInfo.bwdSpeed = "OFF";
                radarInfo.bwdFast = "   ";
            }

            radarInfo.bwdDir = -1;
            radarInfo.bwdFastLocked = false;
            radarInfo.bwdFastSpeed = 0;
        }

        private void ResetLeftAntenna()
        {
            if (radarInfo.llXmit)
            {
                radarInfo.llSpeed = "000";
                radarInfo.llFast = "000";
            }
            else
            {
                radarInfo.llSpeed = "OFF";
                radarInfo.llFast = "   ";
            }

            radarInfo.llFastLocked = false;
            radarInfo.llFastSpeed = 0;
        }

        private void ResetRightAntenna()
        {
            if (radarInfo.llXmit)
            {
                radarInfo.lrSpeed = "000";
                radarInfo.lrFast = "000";
            }
            else
            {
                radarInfo.lrSpeed = "OFF";
                radarInfo.lrFast = "   ";
            }

            radarInfo.lrFastLocked = false;
            radarInfo.lrFastSpeed = 0;
        }

        private void ResetFrontFast()
        {
            if (radarInfo.fwdXmit)
            {
                radarInfo.fwdFast = "000";
                radarInfo.fwdFastSpeed = 0;
                radarInfo.fwdFastLocked = false;

                API.SendNuiMessage(Json.Stringify(new
                {
                    lockfwdfast = false
                }));
            }
        }

        private void ResetRearFast()
        {
            if (radarInfo.bwdXmit)
            {
                radarInfo.bwdFast = "000";
                radarInfo.bwdFastSpeed = 0;
                radarInfo.bwdFastLocked = false;

                API.SendNuiMessage(Json.Stringify(new
                {
                    lockbwdfast = false
                }));
            }
        }

        private void ResetLidarFast()
        {
            if (radarInfo.llXmit)
            {
                radarInfo.llFast = "000";
                radarInfo.llFastSpeed = 0;
                radarInfo.llFastLocked = false;

                radarInfo.lrFast = "000";
                radarInfo.lrFastSpeed = 0;
                radarInfo.lrFastLocked = false;

                API.SendNuiMessage(Json.Stringify(new
                {
                    lockllfast = false,
                    locklrfast = false
                }));
            }
        }

        private void CloseRadarRC()
        {
            API.SendNuiMessage(Json.Stringify(new
            {
                toggleradarrc = true
            }));

            TriggerEvent("wk:toggleMenuControlLock", false);

            API.SetNuiFocus(false, false);
        }

        private void ToggleSpeedType()
        {
            if (radarInfo.speedType == "mph")
            {
                radarInfo.speedType = "kmh";
                Screen.ShowNotification("~b~Speed type set to Km/h.");
            }
            else
            {
                radarInfo.speedType = "mph";
                Screen.ShowNotification("~b~Speed type set to MPH.");
            }
        }

        private void ToggleLockBeep()
        {
            if (radarInfo.lockBeep)
            {
                radarInfo.lockBeep = false;
                Screen.ShowNotification("~b~Radar fast lock beep disabled.");
            }
            else
            {
                radarInfo.lockBeep = true;
                Screen.ShowNotification("~b~Radar fast lock beep enabled.");
            }
        }

        private double GetVehSpeed(Vehicle veh)
        {
            if (radarInfo.speedType == "mph")
            {
                return veh.Speed * 2.236936;
            }
            else
            {
                return veh.Speed * 3.6;
            }
        }

        //
        //  MAIN RADAR TICK
        //
        private void ManageVehicleRadar()
        {
            if (!radarEnabled)
                return;

            Ped player = Game.PlayerPed;

            if (!player.IsSittingInVehicle())
                return;

            Vehicle vehicle = player.CurrentVehicle;

            if (vehicle.ClassType != VehicleClass.Emergency)
                return;

            if (vehicle.Driver != player && vehicle.GetPedOnSeat(VehicleSeat.Passenger) != player)
            {
                TriggerEvent("wk:toggleRadar");
                return;
            }
            // Patrol Speed
            double vehicleSpeed = round(GetVehSpeed(vehicle));
            radarInfo.patrolSpeed = FormatSpeed(vehicleSpeed);

            // Rest of the radar options
            Vector3 vehiclePos = vehicle.Position;
            double h = round(vehicle.Heading);

            // Front Antenna
            if (radarInfo.fwdXmit)
            {
                Vector3 offset = radarInfo.angles[radarInfo.fwdMode];
                Vector3 forwardPosition = API.GetOffsetFromEntityInWorldCoords(vehicle.Handle, offset.X, offset.Y, offset.Z);
                float fwdZ = 0f;
                // Adjust the Z axis for the ground position at the end of the beam
                API.GetGroundZFor_3dCoord(forwardPosition.X, forwardPosition.Y, forwardPosition.Z + 500f, ref fwdZ, false);
                if (forwardPosition.Z < fwdZ && !(fwdZ > vehiclePos.Z + 1f))
                {
                    forwardPosition.Z = fwdZ + 0.5f;
                }
                // Tilt the beam down a little bit to accomidate lower sitting vehicles
                forwardPosition.Z -= 5f;

                if (debugging)
                    API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, forwardPosition.X, forwardPosition.Y, forwardPosition.Z, 3, 169, 252, 0xFF);

                Vehicle fwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, forwardPosition));

                if (!fwdVeh.Exists())
                {
                    if (radarInfo.fwdMode != "opp")
                    {
                        forwardPosition.X += 10f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, forwardPosition.X, forwardPosition.Y, forwardPosition.Z, 3, 169, 252, 0xFF);
                        fwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, forwardPosition));
                    }
                    if (!fwdVeh.Exists())
                    {
                        forwardPosition.X -= 20f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, forwardPosition.X, forwardPosition.Y, forwardPosition.Z, 3, 169, 252, 0xFF);
                        fwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, forwardPosition));
                    }
                }

                if (fwdVeh != null && fwdVeh.Exists())
                {
                    // Player must be able to see the car (not in third person)
                    if (API.HasEntityClearLosToEntity(player.Handle, fwdVeh.Handle, 17))
                    {
                        double fwdVehSpeed = round(GetVehSpeed(fwdVeh));
                        // Radar calibration
                        if (!calibrated)
                        {
                            bool shouldSubtract = new Random().NextDouble() > 0.5;
                            int toChange = new Random().Next(7);
                            if (shouldSubtract)
                                fwdVehSpeed -= toChange;
                            else
                                fwdVehSpeed += toChange;
                        }

                        double fwdVehHeading = round(fwdVeh.Heading);
                        int dir = IsEntityInMyHeading(h, fwdVehHeading, 100);

                        // Check to make sure the car is headed genrally towards/away from the radar beam
                        double myHdg = round(vehicle.Heading);
                        double diff = Math.Abs(myHdg - fwdVehHeading);

                        if (diff > 45 && diff < 135)
                        {
                            API.SendNuiMessage(Json.Stringify(new
                            {
                                patrolspeed = radarInfo.patrolSpeed,
                            }));
                            return;
                        }

                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, fwdVeh.Position.X, fwdVeh.Position.Y, fwdVeh.Position.Z, 255, 255, 5, 0xFF);

                        radarInfo.fwdSpeed = FormatSpeed(Convert.ToDouble(fwdVehSpeed));
                        radarInfo.fwdDir = dir;

                        if (fwdVehSpeed > radarInfo.fastLimit && !radarInfo.fwdFastLocked)
                        {
                            if (radarInfo.lockBeep)
                                Game.PlaySound("Beep_red", "DLC_HEIST_HACKING_SNAKE_SOUNDS");

                            radarInfo.fwdFastSpeed = Convert.ToInt32(fwdVehSpeed);
                            radarInfo.fwdFastLocked = true;

                            API.SendNuiMessage(Json.Stringify(new
                            {
                                lockfwdfast = true
                            }));
                        }
                        radarInfo.fwdFast = FormatSpeed(radarInfo.fwdFastSpeed);
                        radarInfo.fwdPrevVeh = fwdVeh.Handle;
                    }
                }
            }

            if (radarInfo.bwdXmit)
            {
                // Get the end point of the radar beam
                Vector3 offset = radarInfo.angles[radarInfo.bwdMode];
                Vector3 backwardPosition = API.GetOffsetFromEntityInWorldCoords(vehicle.Handle, offset.X, -offset.Y, offset.Z);
                float bwdZ = 0f;
                // Adjust the Z axis for the ground position at the end of the beam
                API.GetGroundZFor_3dCoord(backwardPosition.X, backwardPosition.Y, backwardPosition.Z + 500f, ref bwdZ, false);
                if (backwardPosition.Z < bwdZ && !(bwdZ > vehiclePos.Z + 1f))
                {
                    backwardPosition.Z = bwdZ + 0.5f;
                }
                // Tilt the beam down a little bit to accomidate lower sitting vehicles
                backwardPosition.Z -= 5f;

                if (debugging)
                    API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, backwardPosition.X, backwardPosition.Y, backwardPosition.Z, 3, 169, 252, 0xFF);
                Vehicle bwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, backwardPosition));

                if (!bwdVeh.Exists())
                {
                    if (radarInfo.bwdMode != "opp")
                    {
                        backwardPosition.X += 10f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, backwardPosition.X, backwardPosition.Y, backwardPosition.Z, 3, 169, 252, 0xFF);
                        bwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, backwardPosition));
                    }
                    if (!bwdVeh.Exists())
                    {
                        backwardPosition.X -= 20f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, backwardPosition.X, backwardPosition.Y, backwardPosition.Z, 3, 169, 252, 0xFF);
                        bwdVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, backwardPosition));
                    }
                }

                if (bwdVeh != null && bwdVeh.Exists())
                {
                    // Player must be able to see the car (not in third person)
                    if (API.HasEntityClearLosToEntity(player.Handle, bwdVeh.Handle, 17))
                    {
                        double bwdVehSpeed = round(GetVehSpeed(bwdVeh));
                        // Radar calibration
                        if (!calibrated)
                        {
                            bool shouldSubtract = new Random().NextDouble() > 0.5;
                            int toChange = new Random().Next(7);
                            if (shouldSubtract)
                                bwdVehSpeed -= toChange;
                            else
                                bwdVehSpeed += toChange;
                        }

                        double bwdVehHeading = round(bwdVeh.Heading);
                        int dir = IsEntityInMyHeading(h, bwdVehHeading, 100);

                        double myHdg = round(vehicle.Heading);
                        double diff = Math.Abs(myHdg - bwdVehHeading);

                        // Check to make sure the car is headed genrally towards/away from the radar beam
                        if (diff > 45 && (diff < 135 || diff > 215))
                        {
                            API.SendNuiMessage(Json.Stringify(new
                            {
                                patrolspeed = radarInfo.patrolSpeed,
                            }));
                            return;
                        }

                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, bwdVeh.Position.X, bwdVeh.Position.Y, bwdVeh.Position.Z, 255, 255, 5, 0xFF);

                        radarInfo.bwdSpeed = FormatSpeed(Convert.ToDouble(bwdVehSpeed));
                        radarInfo.bwdDir = dir;

                        if (bwdVehSpeed > radarInfo.fastLimit && !radarInfo.bwdFastLocked)
                        {
                            if (radarInfo.lockBeep)
                                Game.PlaySound("Beep_red", "DLC_HEIST_HACKING_SNAKE_SOUNDS");

                            radarInfo.bwdFastSpeed = Convert.ToInt32(bwdVehSpeed);
                            radarInfo.bwdFastLocked = true;

                            API.SendNuiMessage(Json.Stringify(new
                            {
                                lockbwdfast = true
                            }));
                        }
                        radarInfo.bwdFast = FormatSpeed(radarInfo.bwdFastSpeed);
                        radarInfo.bwdPrevVeh = bwdVeh.Handle;
                    }
                }
            }

            // Lidar Left
            if (radarInfo.llXmit)
            {
                Vector3 offset = radarInfo.angles["ll"];
                Vector3 llPosition = API.GetOffsetFromEntityInWorldCoords(vehicle.Handle, offset.X, offset.Y, offset.Z);
                float llZ = 0f;
                // Adjust the Z axis for the ground position at the end of the beam
                API.GetGroundZFor_3dCoord(llPosition.X, llPosition.Y, llPosition.Z + 500f, ref llZ, false);
                if (llPosition.Z < llZ && !(llZ > vehiclePos.Z + 1f))
                {
                    llPosition.Z = llZ + 0.5f;
                }
                // Tilt the beam down a little bit to accomidate lower sitting vehicles
                llPosition.Z -= 5f;

                if (debugging)
                    API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, llPosition.X, llPosition.Y, llPosition.Z, 3, 169, 252, 0xFF);
                Vehicle llVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, llPosition));

                if (!llVeh.Exists())
                {
                    vehiclePos.Y += 3f;
                    llPosition.Y += 3f;
                    if (debugging)
                        API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, llPosition.X, llPosition.Y, llPosition.Z, 3, 169, 252, 0xFF);
                    llVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, llPosition));
                    if (!llVeh.Exists())
                    {
                        vehiclePos.Y -= 6f;
                        llPosition.Y -= 6f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, llPosition.X, llPosition.Y, llPosition.Z, 3, 169, 252, 0xFF);
                        llVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, llPosition));
                    }
                }

                if (llVeh != null && llVeh.Exists())
                {
                    // Player must be able to see the car (not in third person)
                    if (API.HasEntityClearLosToEntity(player.Handle, llVeh.Handle, 17))
                    {
                        double llVehSpeed = round(GetVehSpeed(llVeh));
                        // Radar calibration
                        if (!calibrated)
                        {
                            bool shouldSubtract = new Random().NextDouble() > 0.5;
                            int toChange = new Random().Next(7);
                            if (shouldSubtract)
                                llVehSpeed -= toChange;
                            else
                                llVehSpeed += toChange;
                        }

                        double llVehHeading = round(llVeh.Heading);
                        double myHdg = round(vehicle.Heading);
                        double diff = Math.Abs(myHdg - llVehHeading);

                        // Check to make sure the car is headed genrally towards/away from the radar beam
                        if (diff < 70 || (diff > 110 && diff < 250) || diff > 290)
                        {
                            API.SendNuiMessage(Json.Stringify(new
                            {
                                patrolspeed = radarInfo.patrolSpeed,
                            }));
                            return;
                        }

                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, llVeh.Position.X, llVeh.Position.Y, llVeh.Position.Z, 255, 255, 5, 0xFF);

                        radarInfo.llSpeed = FormatSpeed(Convert.ToDouble(llVehSpeed));

                        if (llVehSpeed > radarInfo.fastLimit && !radarInfo.llFastLocked)
                        {
                            if (radarInfo.lockBeep)
                                Game.PlaySound("Beep_red", "DLC_HEIST_HACKING_SNAKE_SOUNDS");

                            radarInfo.llFastSpeed = Convert.ToInt32(llVehSpeed);
                            radarInfo.llFastLocked = true;

                            API.SendNuiMessage(Json.Stringify(new
                            {
                                lockllfast = true
                            }));
                        }
                        radarInfo.llFast = FormatSpeed(radarInfo.llFastSpeed);
                        radarInfo.llPrevVeh = llVeh.Handle;
                    }
                }

                // Lidar Right

                vehiclePos = vehicle.Position;
                Vector3 lrPosition = API.GetOffsetFromEntityInWorldCoords(vehicle.Handle, -offset.X, offset.Y, offset.Z);
                float lrZ = 0f;
                // Adjust the Z axis for the ground position at the end of the beam
                API.GetGroundZFor_3dCoord(lrPosition.X, lrPosition.Y, lrPosition.Z + 500f, ref lrZ, false);
                if (lrPosition.Z < lrZ && !(lrZ > vehiclePos.Z + 1f))
                {
                    lrPosition.Z = lrZ + 0.5f;
                }
                // Tilt the beam down a little bit to accomidate lower sitting vehicles
                lrPosition.Z -= 5f;

                if (debugging)
                    API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, lrPosition.X, lrPosition.Y, lrPosition.Z, 3, 169, 252, 0xFF);

                Vehicle lrVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, lrPosition));

                // No vehicle found, check "off" angles
                if (!lrVeh.Exists())
                {
                    vehiclePos.Y += 3f;
                    lrPosition.Y += 3f;
                    if (debugging)
                        API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, lrPosition.X, lrPosition.Y, lrPosition.Z, 3, 169, 252, 0xFF);
                    lrVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, lrPosition));
                    if (!llVeh.Exists())
                    {
                        vehiclePos.Y -= 6f;
                        lrPosition.Y -= 6f;
                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, lrPosition.X, lrPosition.Y, lrPosition.Z, 3, 169, 252, 0xFF);
                        lrVeh = new Vehicle(GetVehicleInDirectionSphere(vehicle.Handle, vehiclePos, lrPosition));
                    }
                }

                // Process the vehicle 
                if (lrVeh != null && lrVeh.Exists())
                {
                    // Player must be able to see the car (not in third person)
                    if (API.HasEntityClearLosToEntity(player.Handle, lrVeh.Handle, 17))
                    {
                        double lrVehSpeed = round(GetVehSpeed(lrVeh));
                        // Radar calibration
                        if (!calibrated)
                        {
                            bool shouldSubtract = new Random().NextDouble() > 0.5;
                            int toChange = new Random().Next(7);
                            if (shouldSubtract)
                                lrVehSpeed -= toChange;
                            else
                                lrVehSpeed += toChange;
                        }

                        double lrVehHeading = round(lrVeh.Heading);
                        double myHdg = round(vehicle.Heading);
                        double diff = Math.Abs(myHdg - lrVehHeading);

                        // Check to make sure the car is headed genrally towards/away from the radar beam
                        if (diff < 70 || (diff > 110 && diff < 250) || diff > 290)
                        {
                            API.SendNuiMessage(Json.Stringify(new
                            {
                                patrolspeed = radarInfo.patrolSpeed,
                            }));
                            return;
                        }

                        if (debugging)
                            API.DrawLine(vehiclePos.X, vehiclePos.Y, vehiclePos.Z, lrVeh.Position.X, lrVeh.Position.Y, lrVeh.Position.Z, 255, 255, 5, 0xFF);

                        radarInfo.lrSpeed = FormatSpeed(Convert.ToDouble(lrVehSpeed));

                        if (lrVehSpeed > radarInfo.fastLimit && !radarInfo.lrFastLocked)
                        {
                            if (radarInfo.lockBeep)
                                Game.PlaySound("Beep_red", "DLC_HEIST_HACKING_SNAKE_SOUNDS");

                            radarInfo.lrFastSpeed = Convert.ToInt32(lrVehSpeed);
                            radarInfo.lrFastLocked = true;

                            API.SendNuiMessage(Json.Stringify(new
                            {
                                locklrfast = true
                            }));
                        }
                        radarInfo.lrFast = FormatSpeed(radarInfo.lrFastSpeed);
                        radarInfo.lrPrevVeh = lrVeh.Handle;
                    }
                }
            }


            API.SendNuiMessage(Json.Stringify(new
            {
                patrolspeed = radarInfo.patrolSpeed,
                fwdspeed = radarInfo.fwdSpeed,
                fwdfast = radarInfo.fwdFast,
                fwddir = radarInfo.fwdDir,
                llspeed = radarInfo.llSpeed,
                llfast = radarInfo.llFast,
                lrspeed = radarInfo.lrSpeed,
                lrfast = radarInfo.lrFast,
                bwdspeed = radarInfo.bwdSpeed,
                bwdfast = radarInfo.bwdFast,
                bwddir = radarInfo.bwdDir
            }));
        }

        private void ManageLidar()
        {
            Ped player = Game.PlayerPed;
            Player p = Game.Player;
            if (player.Weapons.Current == null)
                return;
            if (player.Weapons.Current.Hash != WeaponHash.VintagePistol)
            {
                if (lidarShown)
                {
                    API.SendNuiMessage(Json.Stringify(new
                    {
                        type = "lidar",
                        action = "close"
                    }));
                    lidarShown = false;
                }
                return;
            }
            if (!lidarShown)
            {
                lidarShown = true;
                API.SendNuiMessage(Json.Stringify(new
                {
                    type = "lidar",
                    action = "open"
                }));
            }
            if (!player.IsAiming)
                return;
            
            Game.DisableControlThisFrame(0, Control.Attack);
            player.FiringPattern = FiringPattern.FullAuto;
            Game.DisableControlThisFrame(0, Control.MeleeAttackAlternate);
            p.DisableFiringThisFrame();

            if (Game.IsDisabledControlPressed(0, Control.Attack))
            {
                // Player is shooting a laser
                Vector3 position = player.Position;
                Entity target = p.GetTargetedEntity();

                if (target != null && target.Exists())
                {
                    Vehicle veh = new Vehicle(target.Handle);
                    API.DrawLine(position.X, position.Y, position.Z + 0.5f, target.Position.X, target.Position.Y, target.Position.Z, 255, 10, 10, 0xFF);
                    // Player must be able to see the car (not in third person)
                    if (API.HasEntityClearLosToEntity(player.Handle, veh.Handle, 17))
                    {
                        double vehSpeed = round(GetVehSpeed(veh));

                        API.SendNuiMessage(Json.Stringify(new
                        {
                            type = "lidar",
                            speed = vehSpeed.ToString("000"),
                            range = API.GetDistanceBetweenCoords(position.X, position.Y, position.Z, veh.Position.X, veh.Position.Y, veh.Position.Z, true)
                        }));
                    }
                }
            }
        }

        [EventHandler("wk:nuiCallback")]
        private void NUICallback(string data)
        {
            // Toggle Radar
            if (data == "radar_toggle")
                TriggerEvent("wk:toggleRadar");

            // Front Antenna
            else if (data == "radar_frontopp" && radarInfo.fwdXmit)
            {
                radarInfo.fwdMode = "opp";
                API.SendNuiMessage(Json.Stringify(new
                {
                    fwdmode = radarInfo.fwdMode
                }));
            }
            else if (data == "radar_frontxmit")
            {
                radarInfo.fwdXmit = !radarInfo.fwdXmit;
                ResetFrontAntenna();
                API.SendNuiMessage(Json.Stringify(new
                {
                    fwdxmit = radarInfo.fwdXmit
                }));
                if (radarInfo.fwdXmit)
                    radarInfo.fwdMode = "same";
                else
                    radarInfo.fwdMode = "none";

                API.SendNuiMessage(Json.Stringify(new
                {
                    fwdmode = radarInfo.fwdMode
                }));
            }
            else if (data == "radar_frontsame" && radarInfo.fwdXmit)
            {
                radarInfo.fwdMode = "same";
                API.SendNuiMessage(Json.Stringify(new
                {
                    fwdmode = radarInfo.fwdMode
                }));
            }

            // Rear Antenna
            else if (data == "radar_rearopp" && radarInfo.bwdXmit)
            {
                radarInfo.bwdMode = "opp";
                API.SendNuiMessage(Json.Stringify(new
                {
                    bwdmode = radarInfo.bwdMode
                }));
            }
            else if (data == "radar_rearxmit")
            {
                radarInfo.bwdXmit = !radarInfo.bwdXmit;
                ResetRearAntenna();
                API.SendNuiMessage(Json.Stringify(new
                {
                    bwdxmit = radarInfo.bwdXmit
                }));
                if (radarInfo.bwdXmit)
                    radarInfo.bwdMode = "same";
                else
                    radarInfo.bwdMode = "none";
                API.SendNuiMessage(Json.Stringify(new
                {
                    bwdmode = radarInfo.bwdMode
                }));
            }
            else if (data == "radar_rearsame" && radarInfo.bwdXmit)
            {
                radarInfo.bwdMode = "same";
                API.SendNuiMessage(Json.Stringify(new
                {
                    bwdmode = radarInfo.bwdMode
                }));
            }

            // Lidar
            else if (data == "radar_llxmit")
            {
                radarInfo.llXmit = !radarInfo.llXmit;
                ResetLeftAntenna();
                ResetRightAntenna();
                if (radarInfo.llXmit)
                    API.SendNuiMessage(Json.Stringify(new
                    {
                        llmode = "same"
                    }));
                else
                    API.SendNuiMessage(Json.Stringify(new
                    {
                        llmode = "none"
                    }));
            }

            // Set Fast Limit
            else if (data == "radar_setlimit")
            {
                CloseRadarRC();
                Radar_SetLimit();
            }

            // Speed Type
            else if (data == "radar_speedtype")
            {
                ToggleSpeedType();
            }

            // Lock Beep
            else if (data == "radar_lockbeep")
            {
                ToggleLockBeep();
            }

            // Close
            else if (data == "close")
            {
                CloseRadarRC();
            }
        }

        protected class RadarInfo
        {
            public string patrolSpeed = "000";
            public string speedType = "mph";

            // Front
            public int fwdPrevVeh = 0;
            public bool fwdXmit = true;
            public string fwdMode = "same";
            public string fwdSpeed = "000";
            public string fwdFast = "000";
            public bool fwdFastLocked = false;
            public int fwdDir = -1;
            public int fwdFastSpeed = 0;

            // Lidar Left
            public int llPrevVeh = 0;
            public bool llXmit = true;
            public string llSpeed = "000";
            public string llFast = "000";
            public bool llFastLocked = false;
            public int llFastSpeed = 0;

            // Lidar Right
            public int lrPrevVeh = 0;
            public string lrSpeed = "000";
            public string lrFast = "000";
            public bool lrFastLocked = false;
            public int lrFastSpeed = 0;

            // Rear
            public int bwdPrevVeh = 0;
            public bool bwdXmit = false;
            public string bwdMode = "none";
            public string bwdSpeed = "000";
            public string bwdFast = "000";
            public bool bwdFastLocked = false;
            public int bwdDir = -1;
            public int bwdFastSpeed = 0;

            public int fastResetLimit = 150;
            public int fastLimit = 65;

            public Dictionary<string, Vector3> angles = new Dictionary<string, Vector3>
            {
                { "same", new Vector3(0f, 50f, 0f) },
                { "opp", new Vector3(-10f, 50f, 0f) },
                { "none", new Vector3() },
                { "ll", new Vector3(-90f, 0f, 0f) },
            };

            public bool lockBeep = true;
        }

        public static class Json
        {
            public static T Parse<T>(string json) where T : class
            {
                if (string.IsNullOrWhiteSpace(json)) return null;

                T obj = null;

                try
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    };

                    obj = JsonConvert.DeserializeObject<T>(json, settings);
                }
                catch (Exception ex)
                {
                    // TODO: Find a way to log the exception (can shared libraries do that?!)
                    obj = null;
                }

                return obj;
            }

            public static string Stringify(object data)
            {
                if (data == null) return null;

                string json = null;

                try
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    };

                    json = JsonConvert.SerializeObject(data, settings);
                }
                catch (Exception ex)
                {
                    // TODO: Find a way to log the exception (can shared libraries do that?!)
                    json = null;
                }

                return json;
            }
        }
    }
}
