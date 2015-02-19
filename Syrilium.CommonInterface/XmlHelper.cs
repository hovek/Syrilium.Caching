using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Syrilium.CommonInterface
{
    public class XmlHelper
    {
        private SqlXml sqlXml = null;

        private XmlHelper()
        {
        }

        public static implicit operator XmlHelper(SqlXml sqlXml)
        {
            XmlHelper dbSqlXml = new XmlHelper();
            dbSqlXml.sqlXml = sqlXml;

            return dbSqlXml;
        }

        public static implicit operator SqlXml(XmlHelper dbSqlXml)
        {
            return dbSqlXml.sqlXml;
        }

        public static implicit operator XmlHelper(string xml)
        {
            XmlHelper dbSqlXml = new XmlHelper();

            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(xml);

            XmlReader xmlReader = XmlReader.Create(new StringReader(xmldoc.OuterXml));
            dbSqlXml.sqlXml = new SqlXml(xmlReader);

            return dbSqlXml;
        }

        public static implicit operator string(XmlHelper dbSqlXml)
        {
            return dbSqlXml.sqlXml.Value;
        }

        public static implicit operator XmlDocument(XmlHelper dbSqlXml)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(dbSqlXml.sqlXml.Value);

            return xmldoc;
        }

        public static implicit operator XmlHelper(XmlDocument xmlDocument)
        {
            XmlHelper dbSqlXml = new XmlHelper();

            XmlReader xmlReader = XmlReader.Create(new StringReader(xmlDocument.OuterXml));
            dbSqlXml.sqlXml = new SqlXml(xmlReader);

            return dbSqlXml;
        }
    }
}
