﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SQLFS.Database
{
    public static class ColumnEnumerator
    {
        public static IEnumerable<string> Enumerate(this DatabaseTable table, bool showAutoGenerated = false) => table == null 
            ? throw new ArgumentNullException(nameof(showAutoGenerated)) 
            : Enumerate(table.Columns, showAutoGenerated);

        public static IEnumerable<string> Enumerate(this IReadOnlyDictionary<string, DatabaseColumn> columns, bool showAutoGenerated = false) =>
            columns
                .Where(column =>
                    column.Value != null && 
                    !string.IsNullOrEmpty(column.Key) && 
                    (showAutoGenerated || !column.Value.IsAutoGenerated))
                .Select(column => column.Key);
    }
}
