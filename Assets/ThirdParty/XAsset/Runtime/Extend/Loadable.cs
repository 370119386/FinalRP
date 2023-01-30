namespace VEngine
{
    public partial class Loadable
    {
        public int RefCount => refer.count;
    }
}