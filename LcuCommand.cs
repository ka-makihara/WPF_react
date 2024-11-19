public class PostMcFile
{
    public McLockUnlock? mcLockUnlock { get; set; }
    public Ftp<FileListData>? ftp { get; set; }

    public static string Command(string mcName, int moduleNo, string user, string password, List<string> files)
    {
        string ret = $"{{\"cmd\":\"PostMCFile\"," +
                    $"\"properties\":{{" +
                        $"\"machineName\":\"{mcName}\"," +
                        $"\"moduleNo\":{moduleNo}," +
                        $"\"gantry\":2," +
                        $"\"mcLockUnlock\":{{" +
                            $"\"lock\":{{\"cmdNo\":\"0x01000071\"}}," +
                            $"\"unlock\":{{\"cmdNo\":\"0x01000072\"}}," +
                            $"\"gantry\":1}}," +
                        $"\"ftp\":{{" +
                            $"\"userName\":\"{user}\",\"password\":\"{password}\"," +
                            $"\"data\":[{CreateFileList(files)}]" +
                    "}}}";

        return ret;
    }
    public static McFileList? FromJson(string str)
    {
        McFileList? list = JsonSerializer.Deserialize<McFileList>(str);

        return list;
    }
    private static string CreateFileList(List<string> files)
    {
        string fileList = "";

        foreach (string file in files)
        {
            string mcPath = Path.GetDirectoryName(file);
            string lcuPath = Path.GetFileName(file);
            if (fileList != "")
            {
                fileList += ",";
            }
            fileList += $"{{\"mcPath\":\"{mcPath}\",\"lcuPath\":\"{lcuPath}\"}}";
        }
        fileList = fileList.TrimEnd(',');

        return fileList;
    }
}
