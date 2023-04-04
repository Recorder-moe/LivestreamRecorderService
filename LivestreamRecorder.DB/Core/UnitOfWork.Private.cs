namespace LivestreamRecorder.DB.Core
{
    public class UnitOfWork_Private : UnitOfWork
    {
        public UnitOfWork_Private(PrivateContext privateContext)
            : base(privateContext)
        { }
    }
}
