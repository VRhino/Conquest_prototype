const fs = require('fs');
const path = require('path');

/**
 * Recorre recursivamente una carpeta y aplica una función a cada archivo encontrado.
 */
function walk(dir, callback) {
  fs.readdirSync(dir).forEach((file) => {
    const fullPath = path.join(dir, file);
    if (fs.statSync(fullPath).isDirectory()) {
      walk(fullPath, callback);
    } else {
      callback(fullPath);
    }
  });
}

function renameSystemFiles(rootDir) {
  if (!fs.existsSync(rootDir)) {
    console.error(`❌ Carpeta no encontrada: ${rootDir}`);
    return;
  }

  walk(rootDir, (filePath) => {
    const fileName = path.basename(filePath);
    const dirName = path.dirname(filePath);

    if (fileName.endsWith('System.cs') && !fileName.includes('.System.cs')) {
      const newName = fileName.replace(/System\.cs$/, '.System.cs');
      const newPath = path.join(dirName, newName);
      console.log(`🔄 Renombrando: ${filePath} → ${newPath}`);
      fs.renameSync(filePath, newPath);
    }
  });

  console.log('✅ Renombramiento completado.');
}

// 🧾 Ruta desde la línea de comandos
const targetDir = process.argv[2];

if (!targetDir) {
  console.error('❗ Usá: node rename-Systems.js <ruta-de-carpeta>');
  process.exit(1);
}

renameSystemFiles(path.resolve(targetDir));