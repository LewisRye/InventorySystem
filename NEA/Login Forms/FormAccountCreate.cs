using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Configuration;

namespace NEA
{
    public partial class FormAccountCreate : Form
    {
        private readonly string _connStr = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;

        public FormAccountCreate()
        {
            InitializeComponent();

            var databaseConn = new SqlConnection(_connStr);
            databaseConn.Open();                                                        // connects to database and reads it

            var cmd = new SqlCommand("SELECT AccessLevelID, AccessLevelName FROM AccessLevel", databaseConn); // uses SQL query to read data
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                ComboBoxAccessLevel.Items.Add($"{dr["AccessLevelID"].ToString()}: {dr["AccessLevelName"].ToString()}");               // populates the list from the SQL query
            }
            databaseConn.Close();
        }

        public void CreateAccount(string FirstName, string LastName, string Address, long Phone, int AccessLevel, string Username, string Password)
        {
            bool passwordValid = false;
            string hashedInput = new Hashing().GenerateHash(Password, Username);

            var contains8Characters = new Regex(@".{8,}"); // checks if a string has 8 characters
            var containsUpperCase = new Regex(@"[A-Z]+"); // checks if a string has an upper case char
            var containsLowerCase = new Regex(@"[a-z]+"); // checks if a string has a lower case char
            var containsNumber = new Regex(@"[0-9]+"); // checks if a string has a number
            var containsLegalChars = new Regex(@"^[a-zA-Z0-9- _ = + ! @ # $ % ^ & * ( )]*$"); // checks if a string has all legal characters

            if (contains8Characters.IsMatch(Password)
                        && containsUpperCase.IsMatch(Password)
                        && containsLowerCase.IsMatch(Password)
                        && containsNumber.IsMatch(Password))
            {
                if (containsLegalChars.IsMatch(Password))
                {
                    passwordValid = true;
                }
                else
                {
                    MessageBox.Show("Invalid characters detected...", "Error");
                }
            }
            else
            {
                MessageBox.Show("Password does not meet criteria...", "Error");
            }

            var databaseConn = new SqlConnection(_connStr);

            if (passwordValid)
            {
                try
                {
                    databaseConn.Open();

                    int newAccountId = 0; // creating a new Account requires an AccountIDSSS

                    var profitCmd = new SqlCommand(@"SELECT TOP 1 (AccountID) FROM Account 
                        ORDER BY AccountID DESC; ", databaseConn);
                    var reader = profitCmd.ExecuteReader();

                    while (reader.Read()) // finds an ID suitable for the new account
                    {
                        newAccountId = (Convert.ToInt32(reader[0]) + 1);
                    }

                    databaseConn.Close();

                    databaseConn.Open();

                    var insertAccountCmd = new SqlCommand("INSERT INTO Account VALUES(@AccountID, " +
                        "@User, @PassHash, @AccessLevelID); ", databaseConn);

                    insertAccountCmd.Parameters.AddWithValue("@AccountID", newAccountId);
                    insertAccountCmd.Parameters.AddWithValue("@User", Username);
                    insertAccountCmd.Parameters.AddWithValue("@PassHash", hashedInput);
                    insertAccountCmd.Parameters.AddWithValue("@AccessLevelID", AccessLevel);

                    var insertStaffCmd = new SqlCommand(@"INSERT INTO Staff VALUES((SELECT TOP 1 (StaffID) 
                        FROM Staff ORDER BY StaffID DESC) + 1, @fname, @lname, @address, (SELECT TOP 1 (StaffID) 
                        FROM Staff ORDER BY StaffID DESC) + 1, @phone); ", databaseConn);

                    insertStaffCmd.Parameters.AddWithValue("@fname", FirstName);
                    insertStaffCmd.Parameters.AddWithValue("@lname", LastName);
                    insertStaffCmd.Parameters.AddWithValue("@address", Address);
                    insertStaffCmd.Parameters.AddWithValue("@phone", Phone);

                    insertAccountCmd.ExecuteNonQuery(); // inserts the new account details
                    insertStaffCmd.ExecuteNonQuery(); // inserts the new staff details

                    databaseConn.Close();

                    MessageBox.Show("Account successfully added", "Success");
                    Logon.AccountCreated = true; // tells the Logon class that account has been created
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Unable to connect to the database. " + ex.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
        }

        private void ButtonCreateUser_Click(object Sender, EventArgs E)
        {
            if (TextBoxUsername.Text.Length > 0                                          // ensures all fields have been entered correctly
                && TextBoxPhoneNumber.Text.Length == 11
                && TextBoxPassword.Text.Length > 7)
            {
                CreateAccount(TextBoxFirstName.Text, TextBoxLastName.Text, TextBoxAddress.Text, 
                    Convert.ToInt64(TextBoxPhoneNumber.Text), ComboBoxAccessLevel.SelectedIndex, 
                    TextBoxUsername.Text, TextBoxPassword.Text);
            }

            else
            {
                MessageBox.Show("Not all fields have been entered correctly...", "Error");
            }

            if (Logon.AccountCreated)
            {
                foreach (Form var in Application.OpenForms)
                {
                    var.Visible = true;
                }
                this.Hide();
                this.Close();
                this.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private void ButtonReturnLogin_Click(object Sender, EventArgs E)
        {
            foreach (Form var in Application.OpenForms)
            {
                var.Visible = true;
            }
            Logon.CurrentUser = "";
            Logon.AccessLevel = "";
            this.Hide();
            this.Close();
            this.Dispose();
            GC.Collect(); 
            GC.WaitForPendingFinalizers();
        }

        private void TextBoxPassword_TextChanged(object Sender, EventArgs E)
        {
            ToolTip.SetToolTip(TextBoxPassword, "Passwords must be: \n • at least 8 characters \n" +
                                                "And contain: \n • at least 1 lowercase \n • at least 1 uppercase \n • at least 1 number");
            // ValuePwdStrength.Text = "0%";

            if (TextBoxPassword.Text.Length > 0)
            {
                double newPwdStrength = (TextBoxPassword.Text.Length * 4);

                int numberOfSpecials = Regex.Matches(TextBoxPassword.Text, "[- _ = + ! @ # $ % ^ & * ( ) £]").Count;
                newPwdStrength += (numberOfSpecials*numberOfSpecials) * 1.7;          // number of specials squared, means more specials dramatically improve strength

                int numberOfCapitals = Regex.Matches(TextBoxPassword.Text, "[A-Z]").Count;
                newPwdStrength += (numberOfCapitals*numberOfCapitals) * 1.7;          // number of capitals squared, means more capitals dramatically improve strength

                int numberOfNumbers = Regex.Matches(TextBoxPassword.Text, "[0-9]").Count;
                newPwdStrength += (numberOfNumbers * numberOfNumbers) * 1.7;          // number of numbers squared, means more capitals dramatically improve strength

                if (newPwdStrength > 100)
                {
                    newPwdStrength = 100;                                             // ensures the value never goes above 100%
                }

                ValuePwdStrength.Text = newPwdStrength.ToString() + "%";              // displays password strength % in the text box
                ValuePwdStrength.ForeColor = Color.Red;

                BarPwdStrength.Value = Convert.ToInt16(newPwdStrength);                  // adds strength bar for visual indication of strength

                if (newPwdStrength > 33)
                {
                    ValuePwdStrength.ForeColor = Color.OrangeRed;
                }
                if (newPwdStrength > 40)
                {
                    ValuePwdStrength.ForeColor = Color.SandyBrown;
                }
                if (newPwdStrength > 65)
                {
                    ValuePwdStrength.ForeColor = Color.YellowGreen;
                }
                if (newPwdStrength > 80)
                {
                    ValuePwdStrength.ForeColor = Color.Green;
                }
            }
            else
            {
                ValuePwdStrength.Text = "0%";
            }
        }

        private void LoginCreateForm_Load(object Sender, EventArgs E)
        {

        }

        private void TextBoxFirstName_TextChanged(object sender, EventArgs e)
        {
            Random rnd = new Random();
            int RandomNumber = rnd.Next(100, 999);
            TextBoxUsername.Text = TextBoxFirstName.Text + RandomNumber.ToString();
        }
    }
}
