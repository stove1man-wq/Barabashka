using System.Threading.Tasks;

public interface IAgentBrain
{
    Task<string> ProcessMessageAsync(string userText);
}