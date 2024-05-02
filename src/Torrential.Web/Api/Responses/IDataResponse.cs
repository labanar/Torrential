namespace Torrential.Web.Api.Responses
{
    public interface IDataResponse<T>
    {
        public T? Data { get; }
    }
}
