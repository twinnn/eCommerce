namespace Common.Contract.Builders
{
    public interface IBuilder<in D, out T> where T : class, new()
    {
        public T Build(D data);
    }
}