import { TestBed } from '@angular/core/testing';

import { AreaTablero } from './area-tablero';
import { AreaOcupacion } from '../models/monitoreo.models';

const AREA: AreaOcupacion = {
  espacioId: 'espacio-1',
  nombre: 'Laboratorio A',
  aforo: 20,
  puestosActivos: 20,
  enUso: 12,
  pendientesCheckIn: 3,
  sesionesSinCerrar: 2,
  libres: 5,
  porcentajeOcupacion: 60,
  proximaLiberacion: '2026-11-03T11:00:00-05:00',
  enCurso: [
    {
      reservaId: 'reserva-1',
      puestoIdentificador: 'PC-07',
      titular: 'Ana Titular',
      inicio: '2026-11-03T10:00:00-05:00',
      fin: '2026-11-03T11:00:00-05:00',
      fechaCheckIn: '2026-11-03T10:02:00-05:00'
    }
  ]
};

describe('AreaTablero', () => {
  function crear(area: AreaOcupacion) {
    const fixture = TestBed.createComponent(AreaTablero);
    fixture.componentInstance.area = area;
    fixture.detectChanges();

    return fixture;
  }

  it('muestra los cuatro estados con su conteo y la ocupación del área', () => {
    const fixture = crear(AREA);
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('Laboratorio A');
    expect(texto).toContain('60%');
    expect(texto).toContain('Libre:');
    expect(texto).toContain('En uso:');
    expect(texto).toContain('Pendiente:');
    expect(texto).toContain('No-show:');
    expect(texto).toContain('12');
    expect(texto).toContain('3');
    expect(texto).toContain('5');
    expect(texto).toContain('2');
  });

  it('alerta las sesiones sin cerrar e informa la próxima liberación', () => {
    const fixture = crear(AREA);
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('2 sesión(es) sin cerrar');
    expect(texto).toContain('Próxima liberación: 11:00');
  });

  it('lista las sesiones en curso con puesto, titular e ingreso', () => {
    const fixture = crear(AREA);
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('PC-07');
    expect(texto).toContain('Ana Titular');
    expect(texto).toContain('10:00 - 11:00');
    expect(texto).toContain('Ingreso 10:02');
  });

  it('un área sin puestos activos deja la barra en cero y no alerta', () => {
    const fixture = crear({
      ...AREA,
      aforo: 0,
      puestosActivos: 0,
      enUso: 0,
      pendientesCheckIn: 0,
      sesionesSinCerrar: 0,
      libres: 0,
      porcentajeOcupacion: 0,
      proximaLiberacion: null,
      enCurso: []
    });
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('0%');
    expect(texto).toContain('Sin sesiones en uso en este momento.');
    expect(fixture.nativeElement.querySelector('.alerta')).toBeNull();
    expect(fixture.componentInstance.ancho(5)).toBe(0);
  });
});
