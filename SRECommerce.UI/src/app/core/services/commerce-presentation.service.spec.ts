import { safeThemeVariables } from './commerce-presentation.service';

describe('store theme variables', () => {
  it('applies supported theme tokens and drops external CSS or unsupported properties', () => {
    expect(safeThemeVariables({
      '--color-button-background': '#123456', '--font-family': 'Georgia, serif', '--font-size': '20px', '--heading-scale': '1.5',
      '--color-text': 'url(https://example.test/color)', '--font-weight': '400', '--border-radius': '100px', 'background-image': 'url(https://example.test/x)'
    })).toEqual({ '--color-button-background': '#123456', '--font-family': 'Georgia, serif', '--font-size': '20px', '--heading-scale': '1.5' });
  });
});
