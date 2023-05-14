namespace YARG.Core.Engine.Guitar
{
    public class GuitarEngineParameters : BaseEngineParameters
    {

        public double HopoLeniency  { get; }
        public double StrumLeniency { get; }
        
        public bool AntiGhosting { get; }
        
        public GuitarEngineParameters(double hitWindow, double frontBackRatio, double hopoLeniency, double strumLeniency, bool antiGhosting) 
            : base(hitWindow, frontBackRatio)
        {
            HopoLeniency = hopoLeniency;
            StrumLeniency = strumLeniency;
            
            AntiGhosting = antiGhosting;
        }
    }
}